namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services.Translation;

internal sealed class EntityTranslationRecordImporter
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

        var needUpdate = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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
                string.IsNullOrWhiteSpace(entityLogicalName))
            {
                continue;
            }

            if (!metadata.TryGetValue("Type", out var type) || string.IsNullOrWhiteSpace(type))
            {
                continue;
            }

            var entity = GetOrLoadEntityMetadata(entityLogicalName, metadataCache, service);
            if (entity is null)
            {
                continue;
            }

            switch (type)
            {
                case "DisplayName":
                    entity.DisplayName ??= new Label();
                    SetOrAddLocalizedLabel(entity.DisplayName.LocalizedLabels, lcid, record.TargetText);
                    needUpdate.Add(entity.LogicalName);
                    break;
                case "DisplayCollectionName":
                    entity.DisplayCollectionName ??= new Label();
                    SetOrAddLocalizedLabel(entity.DisplayCollectionName.LocalizedLabels, lcid, record.TargetText);
                    needUpdate.Add(entity.LogicalName);
                    break;
                case "Description":
                    entity.Description ??= new Label();
                    SetOrAddLocalizedLabel(entity.Description.LocalizedLabels, lcid, record.TargetText);
                    needUpdate.Add(entity.LogicalName);
                    break;
            }
        }

        var requests = new List<OrganizationRequest>();
        foreach (var entity in metadataCache.Where(e => e.IsRenameable?.Value == true && needUpdate.Contains(e.LogicalName)))
        {
            var update = new EntityMetadata
            {
                LogicalName = entity.LogicalName,
                DisplayName = entity.DisplayName,
                Description = entity.Description,
                DisplayCollectionName = entity.DisplayCollectionName
            };

            requests.Add(new UpdateEntityRequest { Entity = update, MergeLabels = true });
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
