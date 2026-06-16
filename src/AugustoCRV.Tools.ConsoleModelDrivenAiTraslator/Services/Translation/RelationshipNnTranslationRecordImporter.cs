namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services.Translation;

internal sealed class RelationshipNnTranslationRecordImporter
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

        var relationships = new Dictionary<Guid, ManyToManyRelationshipMetadata>();
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
                !metadata.TryGetValue("Relationship Intersect Entity", out var intersectEntityName) ||
                !metadata.TryGetValue("Relationship Id", out var relationshipIdValue) ||
                string.IsNullOrWhiteSpace(entityLogicalName) ||
                string.IsNullOrWhiteSpace(intersectEntityName) ||
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

                relationship = entity.ManyToManyRelationships.FirstOrDefault(r => r.MetadataId == relationshipId)
                    ?? entity.ManyToManyRelationships.FirstOrDefault(r =>
                        r.IntersectEntityName.Equals(intersectEntityName, StringComparison.OrdinalIgnoreCase));
                if (relationship is null)
                {
                    continue;
                }

                relationships[relationshipId] = relationship;
            }

            LocalizedLabelCollection labels;
            if (relationship.Entity1LogicalName.Equals(entityLogicalName, StringComparison.OrdinalIgnoreCase))
            {
                relationship.Entity1AssociatedMenuConfiguration.Label ??= new Label();
                labels = relationship.Entity1AssociatedMenuConfiguration.Label.LocalizedLabels;
            }
            else if (relationship.Entity2LogicalName.Equals(entityLogicalName, StringComparison.OrdinalIgnoreCase))
            {
                relationship.Entity2AssociatedMenuConfiguration.Label ??= new Label();
                labels = relationship.Entity2AssociatedMenuConfiguration.Label.LocalizedLabels;
            }
            else
            {
                continue;
            }

            var changed = SetOrAddLocalizedLabel(labels, lcid, record.TargetText);
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
