namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services.Translation;

internal sealed class RelationshipNnRecordExporter : BaseRecordExporter
{
    private const string DatasetName = "RelationshipsNN";

    /// <summary>Exports N:N relationship associated menu labels as TranslationRecord objects.</summary>
    public IReadOnlyList<TranslationRecord> Export(
        List<EntityMetadata> entities,
        int sourceLcid,
        IReadOnlyList<int> allLcids,
        ExportSettings settings)
    {
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentNullException.ThrowIfNull(allLcids);
        ArgumentNullException.ThrowIfNull(settings);

        var records = new List<TranslationRecord>();
        var rowNumber = 0;
        var sourceLcidStr = sourceLcid.ToString(CultureInfo.InvariantCulture);

        foreach (var entity in entities.OrderBy(e => e.LogicalName))
        {
            foreach (var rel in entity.ManyToManyRelationships)
            {
                if (!settings.EnableManaged && IsManagedComponent(rel))
                {
                    continue;
                }

                if (!rel.MetadataId.HasValue)
                {
                    continue;
                }

                var amc = rel.Entity1LogicalName == entity.LogicalName
                    ? rel.Entity1AssociatedMenuConfiguration
                    : rel.Entity2AssociatedMenuConfiguration;

                if (!amc.Behavior.HasValue || amc.Behavior.Value != AssociatedMenuBehavior.UseLabel)
                {
                    continue;
                }

                var relationshipId = rel.MetadataId.Value.ToString("B");
                var label = amc.Label;
                var sourceText = label?.LocalizedLabels.FirstOrDefault(l => l.LanguageCode == sourceLcid)?.Label ?? string.Empty;

                rowNumber++;
                var metadataJson = SerializeMetadata(entity.LogicalName, rel.IntersectEntityName, relationshipId);

                foreach (var targetLcid in allLcids)
                {
                    var targetLcidStr = targetLcid.ToString(CultureInfo.InvariantCulture);
                    var targetText = label?.LocalizedLabels.FirstOrDefault(l => l.LanguageCode == targetLcid)?.Label ?? string.Empty;

                    records.Add(new TranslationRecord
                    {
                        Dataset = DatasetName,
                        RecordKey = $"{DatasetName}|{relationshipId}|{entity.LogicalName}|{targetLcidStr}",
                        RowNumber = rowNumber,
                        EntityLogicalName = entity.LogicalName,
                        ObjectId = relationshipId,
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

    private static string SerializeMetadata(string entity, string intersectEntityName, string relationshipId)
        => JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["Entity"] = entity,
            ["Relationship Intersect Entity"] = intersectEntityName,
            ["Relationship Id"] = relationshipId
        });
}
