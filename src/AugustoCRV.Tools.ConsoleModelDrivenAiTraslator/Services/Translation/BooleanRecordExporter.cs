namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services.Translation;

internal sealed class BooleanRecordExporter : BaseRecordExporter
{
    private const string DatasetName = "Booleans";

    /// <summary>Exports boolean attribute option labels and descriptions as TranslationRecord objects.</summary>
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

                if (attribute.AttributeType == null
                    || attribute.AttributeType.Value != AttributeTypeCode.Boolean
                    || !attribute.MetadataId.HasValue)
                {
                    continue;
                }

                var bAmd = (BooleanAttributeMetadata)attribute;

                if (bAmd.OptionSet?.IsGlobal ?? false)
                {
                    continue;
                }

                var attributeId = attribute.MetadataId.Value.ToString("B");

                if (settings.ExportNames)
                {
                    var falseValue = bAmd.OptionSet?.FalseOption?.Value?.ToString(CultureInfo.InvariantCulture) ?? "0";
                    AddRows(records, entity, attribute, attributeId, falseValue, "Label",
                        bAmd.OptionSet?.FalseOption?.Label, sourceLcid, sourceLcidStr, allLcids, ref rowNumber);

                    var trueValue = bAmd.OptionSet?.TrueOption?.Value?.ToString(CultureInfo.InvariantCulture) ?? "1";
                    AddRows(records, entity, attribute, attributeId, trueValue, "Label",
                        bAmd.OptionSet?.TrueOption?.Label, sourceLcid, sourceLcidStr, allLcids, ref rowNumber);
                }

                if (settings.ExportDescriptions)
                {
                    var falseValue = bAmd.OptionSet?.FalseOption?.Value?.ToString(CultureInfo.InvariantCulture) ?? "0";
                    AddRows(records, entity, attribute, attributeId, falseValue, "Description",
                        bAmd.OptionSet?.FalseOption?.Description, sourceLcid, sourceLcidStr, allLcids, ref rowNumber);

                    var trueValue = bAmd.OptionSet?.TrueOption?.Value?.ToString(CultureInfo.InvariantCulture) ?? "1";
                    AddRows(records, entity, attribute, attributeId, trueValue, "Description",
                        bAmd.OptionSet?.TrueOption?.Description, sourceLcid, sourceLcidStr, allLcids, ref rowNumber);
                }
            }
        }

        return records;
    }

    private void AddRows(
        List<TranslationRecord> records,
        EntityMetadata entity,
        AttributeMetadata attribute,
        string attributeId,
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
        var metadataJson = SerializeMetadata(entity.LogicalName, attribute.LogicalName, value, type);

        foreach (var targetLcid in allLcids)
        {
            var targetLcidStr = targetLcid.ToString(CultureInfo.InvariantCulture);
            var targetText = label?.LocalizedLabels.FirstOrDefault(l => l.LanguageCode == targetLcid)?.Label ?? string.Empty;

            records.Add(new TranslationRecord
            {
                Dataset = DatasetName,
                RecordKey = $"{DatasetName}|{attributeId}|{value}|{type}|{targetLcidStr}",
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

    private static string SerializeMetadata(string entityLogicalName, string attributeLogicalName, string value, string type)
        => JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["Entity Logical Name"] = entityLogicalName,
            ["Attribute Logical Name"] = attributeLogicalName,
            ["Value"] = value,
            ["Type"] = type
        });
}
