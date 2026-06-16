namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services.Translation;

internal sealed class AttributeTranslationRecordImporter
{
    public void Import(
        IReadOnlyList<TranslationRecord> records,
        List<EntityMetadata> metadataCache,
        IOrganizationService service,
        bool forceUpdate)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(metadataCache);
        ArgumentNullException.ThrowIfNull(service);

        var updates = new Dictionary<(string EntityLogicalName, string AttributeLogicalName), AttributeMetadata>();

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
            if (!metadata.TryGetValue("Entity Logical Name", out var entityLogicalName) ||
                !metadata.TryGetValue("Attribute Logical Name", out var attributeLogicalName) ||
                !metadata.TryGetValue("Type", out var type) ||
                string.IsNullOrWhiteSpace(entityLogicalName) ||
                string.IsNullOrWhiteSpace(attributeLogicalName) ||
                string.IsNullOrWhiteSpace(type))
            {
                continue;
            }

            var entity = GetOrLoadEntityMetadata(entityLogicalName, metadataCache, service);
            if (entity is null)
            {
                continue;
            }

            var attribute = entity.Attributes.FirstOrDefault(x =>
                x.LogicalName.Equals(attributeLogicalName, StringComparison.OrdinalIgnoreCase));
            if (attribute is null)
            {
                continue;
            }

            switch (type)
            {
                case "DisplayName":
                    attribute.DisplayName ??= new Label();
                    SetOrAddLocalizedLabel(attribute.DisplayName.LocalizedLabels, lcid, record.TargetText);
                    break;
                case "Description":
                    attribute.Description ??= new Label();
                    SetOrAddLocalizedLabel(attribute.Description.LocalizedLabels, lcid, record.TargetText);
                    break;
                default:
                    continue;
            }

            updates[(entity.LogicalName, attribute.LogicalName)] = attribute;
        }

        var requests = new List<OrganizationRequest>();
        foreach (var update in updates.Values)
        {
            if (update.DisplayName is null || update.DisplayName.LocalizedLabels.All(x => string.IsNullOrWhiteSpace(x.Label)))
            {
                continue;
            }

            if (update.IsRenameable?.Value == false)
            {
                continue;
            }

            requests.Add(new UpdateAttributeRequest
            {
                Attribute = update,
                EntityName = update.EntityLogicalName,
                MergeLabels = true
            });
        }

        TranslationRecordImportHelper.ExecuteInBatches(service, requests);
    }

    private static EntityMetadata? GetOrLoadEntityMetadata(
        string logicalName,
        List<EntityMetadata> metadataCache,
        IOrganizationService service)
    {
        var existing = metadataCache.FirstOrDefault(x =>
            x.LogicalName.Equals(logicalName, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return existing;
        }

        var request = new RetrieveEntityRequest
        {
            LogicalName = logicalName,
            EntityFilters = EntityFilters.Entity | EntityFilters.Attributes | EntityFilters.Relationships
        };

        var response = (RetrieveEntityResponse)service.Execute(request);
        var loaded = response.EntityMetadata;
        metadataCache.Add(loaded);
        return loaded;
    }

    private static void SetOrAddLocalizedLabel(LocalizedLabelCollection labels, int lcid, string value)
    {
        var existing = labels.FirstOrDefault(x => x.LanguageCode == lcid);
        if (existing is null)
        {
            labels.Add(new LocalizedLabel(value, lcid));
            return;
        }

        existing.Label = value;
    }
}
