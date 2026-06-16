namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services.Translation;

internal sealed class GlobalOptionSetRecordExporter : BaseRecordExporter
{
    private const string DatasetName = "Global OptionSets";

    /// <summary>Exports global option set labels and descriptions as TranslationRecord objects.</summary>
    public IReadOnlyList<TranslationRecord> Export(
        int sourceLcid,
        IReadOnlyList<int> allLcids,
        IOrganizationService service,
        ExportSettings settings)
    {
        ArgumentNullException.ThrowIfNull(allLcids);
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(settings);

        var records = new List<TranslationRecord>();
        var rowNumber = 0;
        var sourceLcidStr = sourceLcid.ToString(CultureInfo.InvariantCulture);

        var response = (RetrieveAllOptionSetsResponse)service.Execute(new RetrieveAllOptionSetsRequest());
        var omds = response.OptionSetMetadata;

        if (settings.SolutionId != Guid.Empty)
        {
            var oids = service.GetSolutionComponentObjectIds(settings.SolutionId, 9);
            omds = omds.Where(o => oids.Contains(o.MetadataId ?? Guid.Empty)).ToArray();
        }

        foreach (var omd in omds)
        {
            if (!settings.EnableManaged && IsManagedComponent(omd))
            {
                continue;
            }

            if (omd is OptionSetMetadata optionSetMd)
            {
                foreach (var option in optionSetMd.Options.OrderBy(o => o.Value))
                {
                    var valueStr = option.Value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

                    if (settings.ExportNames)
                    {
                        AddRows(records, optionSetMd.Name, valueStr, "Label", option.Label, sourceLcid, sourceLcidStr, allLcids, ref rowNumber);
                    }

                    if (settings.ExportDescriptions)
                    {
                        AddRows(records, optionSetMd.Name, valueStr, "Description", option.Description, sourceLcid, sourceLcidStr, allLcids, ref rowNumber);
                    }
                }
            }
            else if (omd is BooleanOptionSetMetadata boolOmd)
            {
                if (settings.ExportNames)
                {
                    var falseValueStr = boolOmd.FalseOption?.Value?.ToString(CultureInfo.InvariantCulture) ?? "0";
                    AddRows(records, boolOmd.Name, falseValueStr, "Label", boolOmd.FalseOption?.Label, sourceLcid, sourceLcidStr, allLcids, ref rowNumber);

                    var trueValueStr = boolOmd.TrueOption?.Value?.ToString(CultureInfo.InvariantCulture) ?? "1";
                    AddRows(records, boolOmd.Name, trueValueStr, "Label", boolOmd.TrueOption?.Label, sourceLcid, sourceLcidStr, allLcids, ref rowNumber);
                }
            }
        }

        return records;
    }

    private void AddRows(
        List<TranslationRecord> records,
        string optionSetName,
        string value,
        string type,
        Label? label,
        int sourceLcid,
        string sourceLcidStr,
        IReadOnlyList<int> allLcids,
        ref int rowNumber)
    {
        var sourceText = label?.LocalizedLabels.FirstOrDefault(l => l.LanguageCode == sourceLcid)?.Label ?? string.Empty;
        rowNumber++;
        var metadataJson = SerializeMetadata(optionSetName, value, type);

        foreach (var targetLcid in allLcids)
        {
            var targetLcidStr = targetLcid.ToString(CultureInfo.InvariantCulture);
            var targetText = label?.LocalizedLabels.FirstOrDefault(l => l.LanguageCode == targetLcid)?.Label ?? string.Empty;

            records.Add(new TranslationRecord
            {
                Dataset = DatasetName,
                RecordKey = $"{DatasetName}|{optionSetName}|{value}|{type}|{targetLcidStr}",
                RowNumber = rowNumber,
                SourceLcid = sourceLcidStr,
                TargetLcid = targetLcidStr,
                SourceText = sourceText,
                TargetText = targetText,
                Checksum = string.IsNullOrEmpty(targetText) ? string.Empty : CalculateChecksum(targetText),
                MetadataJson = metadataJson
            });
        }
    }

    private static string SerializeMetadata(string optionSetName, string value, string type)
        => JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["OptionSet Name"] = optionSetName,
            ["Value"] = value,
            ["Type"] = type
        });
}
