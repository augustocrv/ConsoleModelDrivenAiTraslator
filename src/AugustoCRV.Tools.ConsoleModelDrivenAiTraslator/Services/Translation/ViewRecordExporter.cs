namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services.Translation;

internal sealed class ViewRecordExporter : BaseRecordExporter
{
    private const string DatasetName = "Views";

    /// <summary>Exports saved query (view) names and descriptions as TranslationRecord objects.</summary>
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

        // Collect all views with their localized names/descriptions
        var crmViews = new List<CrmView>();

        foreach (var entity in entities.OrderBy(e => e.LogicalName))
        {
            if (!entity.MetadataId.HasValue || !entity.ObjectTypeCode.HasValue)
            {
                continue;
            }

            var views = RetrieveViews(entity.ObjectTypeCode.Value, service, settings.EnableManaged);

            foreach (var view in views)
            {
                var crmView = crmViews.FirstOrDefault(cv => cv.Id == view.Id);
                if (crmView == null)
                {
                    crmView = new CrmView
                    {
                        Id = view.Id,
                        Entity = view.GetAttributeValue<string>("returnedtypecode"),
                        Type = view.GetAttributeValue<int>("querytype"),
                        Names = new Dictionary<int, string>(),
                        Descriptions = new Dictionary<int, string>()
                    };
                    crmViews.Add(crmView);
                }

                if (settings.ExportNames)
                {
                    var nameResponse = (RetrieveLocLabelsResponse)service.Execute(new RetrieveLocLabelsRequest
                    {
                        AttributeName = "name",
                        EntityMoniker = new EntityReference("savedquery", view.Id)
                    });
                    foreach (var locLabel in nameResponse.Label.LocalizedLabels)
                    {
                        crmView.Names[locLabel.LanguageCode] = locLabel.Label;
                    }
                }

                if (settings.ExportDescriptions)
                {
                    var descResponse = (RetrieveLocLabelsResponse)service.Execute(new RetrieveLocLabelsRequest
                    {
                        AttributeName = "description",
                        EntityMoniker = new EntityReference("savedquery", view.Id)
                    });
                    foreach (var locLabel in descResponse.Label.LocalizedLabels)
                    {
                        crmView.Descriptions[locLabel.LanguageCode] = locLabel.Label;
                    }
                }
            }
        }

        // Produce records
        foreach (var crmView in crmViews.OrderBy(cv => cv.Entity).ThenBy(cv => cv.Type))
        {
            var viewId = crmView.Id.ToString("B");
            var viewTypeStr = crmView.Type.ToString(CultureInfo.InvariantCulture);

            if (settings.ExportNames)
            {
                var sourceText = crmView.Names.TryGetValue(sourceLcid, out var sn) ? sn : string.Empty;
                rowNumber++;
                var metadataJson = SerializeMetadata(viewId, "Name", crmView.Entity, viewTypeStr);

                foreach (var targetLcid in allLcids)
                {
                    var targetLcidStr = targetLcid.ToString(CultureInfo.InvariantCulture);
                    var targetText = crmView.Names.TryGetValue(targetLcid, out var tn) ? tn : string.Empty;

                    records.Add(new TranslationRecord
                    {
                        Dataset = DatasetName,
                        RecordKey = $"{DatasetName}|{viewId}|Name|{targetLcidStr}",
                        RowNumber = rowNumber,
                        EntityLogicalName = crmView.Entity,
                        ObjectId = viewId,
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
                var sourceText = crmView.Descriptions.TryGetValue(sourceLcid, out var sd) ? sd : string.Empty;
                rowNumber++;
                var metadataJson = SerializeMetadata(viewId, "Description", crmView.Entity, viewTypeStr);

                foreach (var targetLcid in allLcids)
                {
                    var targetLcidStr = targetLcid.ToString(CultureInfo.InvariantCulture);
                    var targetText = crmView.Descriptions.TryGetValue(targetLcid, out var td) ? td : string.Empty;

                    records.Add(new TranslationRecord
                    {
                        Dataset = DatasetName,
                        RecordKey = $"{DatasetName}|{viewId}|Description|{targetLcidStr}",
                        RowNumber = rowNumber,
                        EntityLogicalName = crmView.Entity,
                        ObjectId = viewId,
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

    private static List<Entity> RetrieveViews(int objectTypeCode, IOrganizationService service, bool enableManaged)
    {
        var qba = new QueryByAttribute
        {
            EntityName = "savedquery",
            ColumnSet = new ColumnSet("returnedtypecode", "querytype", "ismanaged")
        };
        qba.Attributes.Add("returnedtypecode");
        qba.Values.Add(objectTypeCode);

        var views = service.RetrieveMultiple(qba).Entities;
        return enableManaged
            ? views.ToList()
            : views.Where(v => !v.GetAttributeValue<bool>("ismanaged")).ToList();
    }

    private static string SerializeMetadata(string viewId, string type, string entityLogicalName, string viewType)
        => JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["View Id"] = viewId,
            ["Type"] = type,
            ["Entity Logical Name"] = entityLogicalName,
            ["ViewType"] = viewType
        });
}
