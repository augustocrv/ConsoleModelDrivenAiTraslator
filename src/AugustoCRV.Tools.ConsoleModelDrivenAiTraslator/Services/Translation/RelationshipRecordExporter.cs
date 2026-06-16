namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services.Translation;

internal sealed class RelationshipRecordExporter : BaseRecordExporter
{
    private const string DatasetName = "Relationships";

    /// <summary>Exports 1:N and N:1 relationship associated menu labels as TranslationRecord objects.</summary>
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
        var exported = new HashSet<Guid>();
        var rowNumber = 0;
        var sourceLcidStr = sourceLcid.ToString(CultureInfo.InvariantCulture);

        foreach (var entity in entities.OrderBy(e => e.LogicalName))
        {
            var relationships = entity.OneToManyRelationships
                .Cast<OneToManyRelationshipMetadata>()
                .Concat(entity.ManyToOneRelationships);

            foreach (var rel in relationships)
            {
                if (!settings.EnableManaged && IsManagedComponent(rel))
                {
                    continue;
                }

                if (!rel.MetadataId.HasValue || !exported.Add(rel.MetadataId.Value))
                {
                    continue;
                }

                if (!rel.AssociatedMenuConfiguration.Behavior.HasValue ||
                    rel.AssociatedMenuConfiguration.Behavior.Value != AssociatedMenuBehavior.UseLabel)
                {
                    continue;
                }

                var relationshipId = rel.MetadataId.Value.ToString("B");
                var label = rel.AssociatedMenuConfiguration.Label;
                var sourceText = label?.LocalizedLabels.FirstOrDefault(l => l.LanguageCode == sourceLcid)?.Label ?? string.Empty;

                rowNumber++;
                var metadataJson = SerializeMetadata(rel.ReferencedEntity, rel.SchemaName, relationshipId);

                foreach (var targetLcid in allLcids)
                {
                    var targetLcidStr = targetLcid.ToString(CultureInfo.InvariantCulture);
                    var targetText = label?.LocalizedLabels.FirstOrDefault(l => l.LanguageCode == targetLcid)?.Label ?? string.Empty;

                    records.Add(new TranslationRecord
                    {
                        Dataset = DatasetName,
                        RecordKey = $"{DatasetName}|{relationshipId}|{targetLcidStr}",
                        RowNumber = rowNumber,
                        EntityLogicalName = rel.ReferencedEntity,
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

    private static string SerializeMetadata(string entity, string relationshipName, string relationshipId)
        => JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["Entity"] = entity,
            ["Relationship Name"] = relationshipName,
            ["Relationship Id"] = relationshipId
        });
}
