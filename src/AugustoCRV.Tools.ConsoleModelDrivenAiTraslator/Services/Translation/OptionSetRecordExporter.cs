namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services.Translation;

internal sealed class OptionSetRecordExporter : BaseRecordExporter
{
    private const string DatasetName = "OptionSets";

    /// <summary>Exports local (non-global) option set labels and descriptions as TranslationRecord objects.</summary>
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

                if (attribute.AttributeType == null || !attribute.MetadataId.HasValue)
                {
                    continue;
                }

                OptionSetMetadata? omd = GetOptionSetMetadata(attribute);
                if (omd == null || omd.IsGlobal == true)
                {
                    continue;
                }

                var options = omd.Options;
                if (options == null)
                {
                    continue;
                }

                var attributeId = attribute.MetadataId.Value.ToString("B");

                foreach (var option in options.OrderBy(o => o.Value))
                {
                    var valueStr = option.Value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

                    if (settings.ExportNames)
                    {
                        AddRows(records, entity, attribute, attributeId, valueStr, "Label", option.Label, sourceLcid, sourceLcidStr, allLcids, ref rowNumber);
                    }

                    if (settings.ExportDescriptions)
                    {
                        AddRows(records, entity, attribute, attributeId, valueStr, "Description", option.Description, sourceLcid, sourceLcidStr, allLcids, ref rowNumber);
                    }
                }
            }
        }

        return records;
    }

    private static OptionSetMetadata? GetOptionSetMetadata(AttributeMetadata attribute)
    {
        return attribute.AttributeType!.Value switch
        {
            AttributeTypeCode.Picklist => ((PicklistAttributeMetadata)attribute).OptionSet,
            AttributeTypeCode.State => ((StateAttributeMetadata)attribute).OptionSet,
            AttributeTypeCode.Status => ((StatusAttributeMetadata)attribute).OptionSet,
            AttributeTypeCode.Virtual when attribute is MultiSelectPicklistAttributeMetadata mspl => mspl.OptionSet,
            _ => null
        };
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
