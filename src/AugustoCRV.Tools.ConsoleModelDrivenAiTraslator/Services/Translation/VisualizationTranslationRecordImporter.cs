namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services.Translation;

internal sealed class VisualizationTranslationRecordImporter
{
    public void Import(IReadOnlyList<TranslationRecord> records, IOrganizationService service, bool forceUpdate)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(service);

        var requests = new Dictionary<(Guid ChartId, string AttributeName), SetLocLabelsRequest>();
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
            if (!metadata.TryGetValue("Chart Id", out var chartIdValue) ||
                !Guid.TryParse(chartIdValue, out var chartId) ||
                !metadata.TryGetValue("Type", out var rowType) ||
                string.IsNullOrWhiteSpace(rowType))
            {
                continue;
            }

            var attributeName = rowType.Equals("Name", StringComparison.OrdinalIgnoreCase)
                ? "name"
                : rowType.Equals("Description", StringComparison.OrdinalIgnoreCase)
                    ? "description"
                    : string.Empty;

            if (string.IsNullOrWhiteSpace(attributeName))
            {
                continue;
            }

            var key = (chartId, attributeName);
            if (!requests.TryGetValue(key, out var request))
            {
                var current = (RetrieveLocLabelsResponse)service.Execute(new RetrieveLocLabelsRequest
                {
                    EntityMoniker = new EntityReference("savedqueryvisualization", chartId),
                    AttributeName = attributeName
                });

                request = new SetLocLabelsRequest
                {
                    RequestId = Guid.NewGuid(),
                    EntityMoniker = new EntityReference("savedqueryvisualization", chartId),
                    AttributeName = attributeName,
                    Labels = current.Label.LocalizedLabels.ToArray()
                };

                requests[key] = request;
            }

            var labels = request.Labels.ToList();
            var changed = SetOrAddLocalizedLabel(labels, lcid, record.TargetText);
            if (changed)
            {
                request.Labels = labels.ToArray();
                if (request.RequestId.HasValue)
                {
                    needUpdate.Add(request.RequestId.Value);
                }
            }
        }

        var updates = requests.Values
            .Where(r => r.RequestId.HasValue && needUpdate.Contains(r.RequestId.Value))
            .Cast<OrganizationRequest>()
            .ToList();

        TranslationRecordImportHelper.ExecuteInBatches(service, updates);
    }

    private static bool SetOrAddLocalizedLabel(List<LocalizedLabel> labels, int lcid, string value)
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
