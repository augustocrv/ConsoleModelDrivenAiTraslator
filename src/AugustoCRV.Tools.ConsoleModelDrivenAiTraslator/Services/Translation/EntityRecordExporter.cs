namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services.Translation;

internal sealed class EntityRecordExporter : BaseRecordExporter
{
    private const string DatasetName = "Entities";

    /// <summary>Exports entity display names, collection names, and descriptions as TranslationRecord objects.</summary>
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
            if (!entity.MetadataId.HasValue)
            {
                continue;
            }

            if (!settings.EnableManaged && IsManagedComponent(entity))
            {
                continue;
            }

            var entityId = entity.MetadataId.Value.ToString("B");

            if (settings.ExportNames)
            {
                AddRows(records, entity, entityId, sourceLcid, sourceLcidStr, allLcids, "DisplayName", entity.DisplayName, ref rowNumber);
                AddRows(records, entity, entityId, sourceLcid, sourceLcidStr, allLcids, "DisplayCollectionName", entity.DisplayCollectionName, ref rowNumber);
            }

            if (settings.ExportDescriptions)
            {
                AddRows(records, entity, entityId, sourceLcid, sourceLcidStr, allLcids, "Description", entity.Description, ref rowNumber);
            }
        }

        return records;
    }

    private void AddRows(
        List<TranslationRecord> records,
        EntityMetadata entity,
        string entityId,
        int sourceLcid,
        string sourceLcidStr,
        IReadOnlyList<int> allLcids,
        string type,
        Label? label,
        ref int rowNumber)
    {
        var sourceText = label?.LocalizedLabels.FirstOrDefault(l => l.LanguageCode == sourceLcid)?.Label ?? string.Empty;
        rowNumber++;

        var metadataJson = SerializeMetadata(entity.LogicalName, type);

        foreach (var targetLcid in allLcids)
        {
            var targetLcidStr = targetLcid.ToString(CultureInfo.InvariantCulture);
            var targetText = label?.LocalizedLabels.FirstOrDefault(l => l.LanguageCode == targetLcid)?.Label ?? string.Empty;

            records.Add(new TranslationRecord
            {
                Dataset = DatasetName,
                RecordKey = $"{DatasetName}|{entityId}|{type}|{targetLcidStr}",
                RowNumber = rowNumber,
                EntityLogicalName = entity.LogicalName,
                ObjectId = entityId,
                FieldLogicalName = type,
                SourceLcid = sourceLcidStr,
                TargetLcid = targetLcidStr,
                SourceText = sourceText,
                TargetText = targetText,
                Checksum = string.IsNullOrEmpty(targetText) ? string.Empty : CalculateChecksum(targetText),
                MetadataJson = metadataJson
            });
        }
    }

    private static string SerializeMetadata(string entityLogicalName, string type)
        => JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["Entity Logical Name"] = entityLogicalName,
            ["Type"] = type
        });
}
