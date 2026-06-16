namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services.Translation;

internal sealed class AttributeRecordExporter : BaseRecordExporter
{
    private const string DatasetName = "Attributes";

    /// <summary>Exports attribute display names and descriptions as TranslationRecord objects.</summary>
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
            foreach (var attribute in entity.Attributes.OrderBy(a => a.LogicalName))
            {
                if (!settings.EnableManaged && IsManagedComponent(attribute))
                {
                    continue;
                }

                if (!ShouldExportAttribute(attribute))
                {
                    continue;
                }

                if (attribute.DisplayName != null &&
                    attribute.DisplayName.LocalizedLabels.All(l => string.IsNullOrEmpty(l.Label)))
                {
                    continue;
                }

                var attributeId = attribute.MetadataId!.Value.ToString("B");

                if (settings.ExportNames)
                {
                    AddRows(records, entity, attribute, attributeId, sourceLcid, sourceLcidStr, allLcids, "DisplayName", attribute.DisplayName, ref rowNumber);
                }

                if (settings.ExportDescriptions)
                {
                    AddRows(records, entity, attribute, attributeId, sourceLcid, sourceLcidStr, allLcids, "Description", attribute.Description, ref rowNumber);
                }
            }
        }

        return records;
    }

    private static bool ShouldExportAttribute(AttributeMetadata attribute)
    {
        if (attribute.AttributeType == null || !attribute.MetadataId.HasValue || !attribute.IsRenameable.Value)
        {
            return false;
        }

        if (attribute.AttributeOf != null)
        {
            return false;
        }

        var attrType = attribute.AttributeType.Value;
        if (attrType == AttributeTypeCode.BigInt
            || attrType == AttributeTypeCode.CalendarRules
            || attrType == AttributeTypeCode.EntityName
            || attrType == AttributeTypeCode.ManagedProperty
            || attrType == AttributeTypeCode.Uniqueidentifier
            || (attrType == AttributeTypeCode.Virtual && attribute is not MultiSelectPicklistAttributeMetadata))
        {
            return false;
        }

        // Skip calculated field derivatives
        if (attribute.LogicalName.EndsWith("_state", StringComparison.Ordinal))
        {
            var baseName = attribute.LogicalName[..^6];
            if (attribute.EntityLogicalName != null)
            {
                return true; // let the caller filter by entity
            }
        }

        return true;
    }

    private void AddRows(
        List<TranslationRecord> records,
        EntityMetadata entity,
        AttributeMetadata attribute,
        string attributeId,
        int sourceLcid,
        string sourceLcidStr,
        IReadOnlyList<int> allLcids,
        string type,
        Label? label,
        ref int rowNumber)
    {
        var sourceText = label?.LocalizedLabels.FirstOrDefault(l => l.LanguageCode == sourceLcid)?.Label ?? string.Empty;
        rowNumber++;

        var metadataJson = SerializeMetadata(entity.LogicalName, attribute.LogicalName, type);

        foreach (var targetLcid in allLcids)
        {
            var targetLcidStr = targetLcid.ToString(CultureInfo.InvariantCulture);
            var targetText = label?.LocalizedLabels.FirstOrDefault(l => l.LanguageCode == targetLcid)?.Label ?? string.Empty;

            records.Add(new TranslationRecord
            {
                Dataset = DatasetName,
                RecordKey = $"{DatasetName}|{attributeId}|{type}|{targetLcidStr}",
                RowNumber = rowNumber,
                EntityLogicalName = entity.LogicalName,
                ObjectId = attributeId,
                FieldLogicalName = attribute.LogicalName,
                SourceLcid = sourceLcidStr,
                TargetLcid = targetLcidStr,
                SourceText = sourceText,
                TargetText = targetText,
                Checksum = string.IsNullOrEmpty(targetText) ? string.Empty : CalculateChecksum(targetText),
                MetadataJson = metadataJson
            });
        }
    }

    private static string SerializeMetadata(string entityLogicalName, string attributeLogicalName, string type)
        => JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["Entity Logical Name"] = entityLogicalName,
            ["Attribute Logical Name"] = attributeLogicalName,
            ["Type"] = type
        });
}
