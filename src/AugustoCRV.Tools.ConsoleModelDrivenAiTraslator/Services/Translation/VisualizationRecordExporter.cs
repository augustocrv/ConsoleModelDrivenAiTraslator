namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services.Translation;

internal sealed class VisualizationRecordExporter : BaseRecordExporter
{
    private const string DatasetName = "Charts";

    /// <summary>Exports saved query visualization (chart) names and descriptions as TranslationRecord objects.</summary>
    public IReadOnlyList<TranslationRecord> Export(
        List<EntityMetadata> entities,
        int sourceLcid,
        IReadOnlyList<int> allLcids,
        IOrganizationService service,
        ExportSettings settings)
    {
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentNullException.ThrowIfNull(allLcids);
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(settings);

        var records = new List<TranslationRecord>();
        var rowNumber = 0;
        var sourceLcidStr = sourceLcid.ToString(CultureInfo.InvariantCulture);

        var crmVisualizations = new List<CrmVisualization>();

        foreach (var entity in entities.OrderBy(e => e.LogicalName))
        {
            if (!entity.MetadataId.HasValue || !entity.ObjectTypeCode.HasValue)
            {
                continue;
            }

            var visualizations = RetrieveVisualizations(entity.ObjectTypeCode.Value, service, settings.EnableManaged);

            foreach (var visualization in visualizations)
            {
                var crmVisualization = crmVisualizations.FirstOrDefault(cv => cv.Id == visualization.Id);
                if (crmVisualization == null)
                {
                    crmVisualization = new CrmVisualization
                    {
                        Id = visualization.Id,
                        Entity = visualization.GetAttributeValue<string>("primaryentitytypecode"),
                        Names = new Dictionary<int, string>(),
                        Descriptions = new Dictionary<int, string>()
                    };
                    crmVisualizations.Add(crmVisualization);
                }

                if (settings.ExportNames)
                {
                    var nameResponse = (RetrieveLocLabelsResponse)service.Execute(new RetrieveLocLabelsRequest
                    {
                        AttributeName = "name",
                        EntityMoniker = new EntityReference("savedqueryvisualization", visualization.Id)
                    });
                    foreach (var locLabel in nameResponse.Label.LocalizedLabels)
                    {
                        crmVisualization.Names[locLabel.LanguageCode] = locLabel.Label;
                    }
                }

                if (settings.ExportDescriptions)
                {
                    var descResponse = (RetrieveLocLabelsResponse)service.Execute(new RetrieveLocLabelsRequest
                    {
                        AttributeName = "description",
                        EntityMoniker = new EntityReference("savedqueryvisualization", visualization.Id)
                    });
                    foreach (var locLabel in descResponse.Label.LocalizedLabels)
                    {
                        crmVisualization.Descriptions[locLabel.LanguageCode] = locLabel.Label;
                    }
                }
            }
        }

        foreach (var crmVisualization in crmVisualizations.OrderBy(cv => cv.Entity))
        {
            var chartId = crmVisualization.Id.ToString("B");

            if (settings.ExportNames)
            {
                var sourceText = crmVisualization.Names.TryGetValue(sourceLcid, out var sn) ? sn : string.Empty;
                rowNumber++;
                var metadataJson = SerializeMetadata(chartId, "Name", crmVisualization.Entity);

                foreach (var targetLcid in allLcids)
                {
                    var targetLcidStr = targetLcid.ToString(CultureInfo.InvariantCulture);
                    var targetText = crmVisualization.Names.TryGetValue(targetLcid, out var tn) ? tn : string.Empty;

                    records.Add(new TranslationRecord
                    {
                        Dataset = DatasetName,
                        RecordKey = $"{DatasetName}|{chartId}|Name|{targetLcidStr}",
                        RowNumber = rowNumber,
                        EntityLogicalName = crmVisualization.Entity,
                        ObjectId = chartId,
                        FieldLogicalName = "Name",
                        SourceLcid = sourceLcidStr,
                        TargetLcid = targetLcidStr,
                        SourceText = sourceText,
                        TargetText = targetText,
                        Checksum = string.IsNullOrEmpty(targetText) ? string.Empty : CalculateChecksum(targetText),
                        MetadataJson = metadataJson
                    });
                }
            }

            if (settings.ExportDescriptions)
            {
                var sourceText = crmVisualization.Descriptions.TryGetValue(sourceLcid, out var sd) ? sd : string.Empty;
                rowNumber++;
                var metadataJson = SerializeMetadata(chartId, "Description", crmVisualization.Entity);

                foreach (var targetLcid in allLcids)
                {
                    var targetLcidStr = targetLcid.ToString(CultureInfo.InvariantCulture);
                    var targetText = crmVisualization.Descriptions.TryGetValue(targetLcid, out var td) ? td : string.Empty;

                    records.Add(new TranslationRecord
                    {
                        Dataset = DatasetName,
                        RecordKey = $"{DatasetName}|{chartId}|Description|{targetLcidStr}",
                        RowNumber = rowNumber,
                        EntityLogicalName = crmVisualization.Entity,
                        ObjectId = chartId,
                        FieldLogicalName = "Description",
                        SourceLcid = sourceLcidStr,
                        TargetLcid = targetLcidStr,
                        SourceText = sourceText,
                        TargetText = targetText,
                        Checksum = string.IsNullOrEmpty(targetText) ? string.Empty : CalculateChecksum(targetText),
                        MetadataJson = metadataJson
                    });
                }
            }
        }

        return records;
    }

    private static List<Entity> RetrieveVisualizations(int objectTypeCode, IOrganizationService service, bool enableManaged)
    {
        var qba = new QueryByAttribute
        {
            EntityName = "savedqueryvisualization",
            ColumnSet = new ColumnSet(true)
        };
        qba.Attributes.Add("primaryentitytypecode");
        qba.Values.Add(objectTypeCode);

        var visualizations = service.RetrieveMultiple(qba).Entities;
        return enableManaged
            ? visualizations.ToList()
            : visualizations.Where(v => !v.GetAttributeValue<bool>("ismanaged")).ToList();
    }

    private static string SerializeMetadata(string chartId, string type, string entityLogicalName)
        => JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["Chart Id"] = chartId,
            ["Type"] = type,
            ["Entity Logical Name"] = entityLogicalName
        });
}
