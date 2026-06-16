namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services.Translation;

internal sealed class OptionSetTranslationRecordImporter
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

        var requests = new Dictionary<(string Entity, string Attribute, int Value), UpdateOptionValueRequest>();
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
            if (!metadata.TryGetValue("Entity Logical Name", out var entityLogicalName) ||
                !metadata.TryGetValue("Attribute Logical Name", out var attributeLogicalName) ||
                !metadata.TryGetValue("Value", out var optionValueString) ||
                !metadata.TryGetValue("Type", out var rowType) ||
                string.IsNullOrWhiteSpace(entityLogicalName) ||
                string.IsNullOrWhiteSpace(attributeLogicalName) ||
                string.IsNullOrWhiteSpace(optionValueString) ||
                string.IsNullOrWhiteSpace(rowType) ||
                !int.TryParse(optionValueString, NumberStyles.Integer, CultureInfo.InvariantCulture, out var optionValue))
            {
                continue;
            }

            var entity = GetOrLoadEntityMetadata(entityLogicalName, metadataCache, service);
            if (entity is null)
            {
                continue;
            }

            var option = GetOptionMetadata(entity, attributeLogicalName, optionValue);
            if (option is null)
            {
                continue;
            }

            var key = (entityLogicalName, attributeLogicalName, optionValue);
            if (!requests.TryGetValue(key, out var request))
            {
                request = new UpdateOptionValueRequest
                {
                    RequestId = Guid.NewGuid(),
                    AttributeLogicalName = attributeLogicalName,
                    EntityLogicalName = entityLogicalName,
                    Value = optionValue,
                    Label = option.Label ?? new Label(),
                    Description = option.Description ?? new Label(),
                    MergeLabels = true
                };

                requests[key] = request;
            }

            var changed = rowType.Equals("Label", StringComparison.OrdinalIgnoreCase)
                ? SetOrAddLocalizedLabel(request.Label.LocalizedLabels, lcid, record.TargetText)
                : rowType.Equals("Description", StringComparison.OrdinalIgnoreCase) &&
                  SetOrAddLocalizedLabel(request.Description.LocalizedLabels, lcid, record.TargetText);

            if (changed && request.RequestId.HasValue)
            {
                needUpdate.Add(request.RequestId.Value);
            }
        }

        var updates = requests.Values
            .Where(r => r.RequestId.HasValue && needUpdate.Contains(r.RequestId.Value))
            .Cast<OrganizationRequest>()
            .ToList();

        TranslationRecordImportHelper.ExecuteInBatches(service, updates);
    }

    private static OptionMetadata? GetOptionMetadata(EntityMetadata entity, string attributeLogicalName, int optionValue)
    {
        var attribute = entity.Attributes.FirstOrDefault(a =>
            a.LogicalName.Equals(attributeLogicalName, StringComparison.OrdinalIgnoreCase));

        OptionSetMetadata? optionSet = attribute switch
        {
            PicklistAttributeMetadata picklist => picklist.OptionSet,
            StateAttributeMetadata state => state.OptionSet,
            StatusAttributeMetadata status => status.OptionSet,
            MultiSelectPicklistAttributeMetadata multi => multi.OptionSet,
            _ => null
        };

        return optionSet?.Options.FirstOrDefault(o => o.Value == optionValue);
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
