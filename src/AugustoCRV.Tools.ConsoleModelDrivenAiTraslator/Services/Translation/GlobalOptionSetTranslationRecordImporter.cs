namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services.Translation;

internal sealed class GlobalOptionSetTranslationRecordImporter
{
    public void Import(IReadOnlyList<TranslationRecord> records, IOrganizationService service, bool forceUpdate)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(service);

        var requests = new Dictionary<(string OptionSetName, int Value), UpdateOptionValueRequest>();
        var needUpdate = new HashSet<Guid>();

        foreach (var record in records)
        {
            if (string.IsNullOrWhiteSpace(record.TargetLcid) || string.IsNullOrWhiteSpace(record.TargetText))
            {
                continue;
            }

            if (!int.TryParse(record.TargetLcid, NumberStyles.Integer, CultureInfo.InvariantCulture, out var lcid))
            {
                continue;
            }

            if (!forceUpdate && TranslationRecordImportHelper.IsChecksumUnchanged(record.Checksum, record.TargetText))
            {
                continue;
            }

            var metadata = TranslationRecordImportHelper.DeserializeMetadata(record.MetadataJson);
            if (!metadata.TryGetValue("OptionSet Name", out var optionSetName) ||
                !metadata.TryGetValue("Value", out var optionValueString) ||
                !metadata.TryGetValue("Type", out var rowType) ||
                string.IsNullOrWhiteSpace(optionSetName) ||
                string.IsNullOrWhiteSpace(optionValueString) ||
                string.IsNullOrWhiteSpace(rowType) ||
                !int.TryParse(optionValueString, NumberStyles.Integer, CultureInfo.InvariantCulture, out var optionValue))
            {
                continue;
            }

            var key = (optionSetName, optionValue);
            if (!requests.TryGetValue(key, out var request))
            {
                var md = ((RetrieveOptionSetResponse)service.Execute(new RetrieveOptionSetRequest
                {
                    Name = optionSetName,
                    RetrieveAsIfPublished = true
                })).OptionSetMetadata;

                var option = md switch
                {
                    OptionSetMetadata optionSetMetadata => optionSetMetadata.Options.FirstOrDefault(o => o.Value == optionValue),
                    BooleanOptionSetMetadata booleanOptionSetMetadata => optionValue == 0 ? booleanOptionSetMetadata.FalseOption : booleanOptionSetMetadata.TrueOption,
                    _ => null
                };

                request = new UpdateOptionValueRequest
                {
                    RequestId = Guid.NewGuid(),
                    OptionSetName = optionSetName,
                    Value = optionValue,
                    Label = option?.Label ?? new Label(),
                    Description = option?.Description ?? new Label(),
                    MergeLabels = true
                };

                requests[key] = request;
            }

            var changed = rowType.Equals("Label", StringComparison.OrdinalIgnoreCase)
                ? SetOrAddLocalizedLabel(request.Label.LocalizedLabels, lcid, record.TargetText)
                : rowType.Equals("Description", StringComparison.OrdinalIgnoreCase) &&
                  SetOrAddLocalizedLabel(request.Description.LocalizedLabels, lcid, record.TargetText);

            if (changed && request.RequestId.HasValue)
            {
                needUpdate.Add(request.RequestId.Value);
            }
        }

        var updates = requests.Values
            .Where(r => r.RequestId.HasValue && needUpdate.Contains(r.RequestId.Value))
            .Cast<OrganizationRequest>()
            .ToList();

        TranslationRecordImportHelper.ExecuteInBatches(service, updates);
    }

    private static bool SetOrAddLocalizedLabel(LocalizedLabelCollection labels, int lcid, string value)
    {
        var existing = labels.FirstOrDefault(x => x.LanguageCode == lcid);
        if (existing is null)
        {
            labels.Add(new LocalizedLabel(value, lcid));
            return true;
        }

        if (string.Equals(existing.Label, value, StringComparison.Ordinal))
        {
            return false;
        }

        existing.Label = value;
        return true;
    }
}
