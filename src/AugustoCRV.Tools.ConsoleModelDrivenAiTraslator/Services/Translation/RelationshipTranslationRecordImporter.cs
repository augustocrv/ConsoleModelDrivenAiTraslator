namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services.Translation;

internal sealed class RelationshipTranslationRecordImporter
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

        var relationships = new Dictionary<Guid, OneToManyRelationshipMetadata>();
        var needUpdate = new HashSet<Guid>();

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
            if (!metadata.TryGetValue("Entity", out var entityLogicalName) ||
                !metadata.TryGetValue("Relationship Name", out var relationshipSchemaName) ||
                !metadata.TryGetValue("Relationship Id", out var relationshipIdValue) ||
                string.IsNullOrWhiteSpace(entityLogicalName) ||
                string.IsNullOrWhiteSpace(relationshipSchemaName) ||
                !Guid.TryParse(relationshipIdValue, out var relationshipId))
            {
                continue;
            }

            if (!relationships.TryGetValue(relationshipId, out var relationship))
            {
                var entity = GetOrLoadEntityMetadata(entityLogicalName, metadataCache, service);
                if (entity is null)
                {
                    continue;
                }

                relationship = entity.OneToManyRelationships.FirstOrDefault(r => r.MetadataId == relationshipId)
                    ?? entity.ManyToOneRelationships.FirstOrDefault(r => r.MetadataId == relationshipId)
                    ?? entity.OneToManyRelationships.FirstOrDefault(r =>
                        r.SchemaName.Equals(relationshipSchemaName, StringComparison.OrdinalIgnoreCase))
                    ?? entity.ManyToOneRelationships.FirstOrDefault(r =>
                        r.SchemaName.Equals(relationshipSchemaName, StringComparison.OrdinalIgnoreCase));

                if (relationship is null)
                {
                    continue;
                }

                relationships[relationshipId] = relationship;
            }

            relationship.AssociatedMenuConfiguration.Label ??= new Label();
            var changed = SetOrAddLocalizedLabel(relationship.AssociatedMenuConfiguration.Label.LocalizedLabels, lcid, record.TargetText);
            if (changed)
            {
                needUpdate.Add(relationshipId);
            }
        }

        var requests = relationships
            .Where(kvp => needUpdate.Contains(kvp.Key))
            .Select(static kvp => (OrganizationRequest)new UpdateRelationshipRequest
            {
                Relationship = kvp.Value,
                MergeLabels = true
            })
            .ToList();

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

    private static bool SetOrAddLocalizedLabel(LocalizedLabelCollection labels, int lcid, string value)
    {
        var existing = labels.FirstOrDefault(x => x.LanguageCode == lcid);
        if (existing is null)
        {
            labels.Add(new LocalizedLabel(value, lcid));
            return true;
        }

        if (string.Equals(existing.Label, value, StringComparison.Ordinal))
        {
            return false;
        }

        existing.Label = value;
        return true;
    }
}
