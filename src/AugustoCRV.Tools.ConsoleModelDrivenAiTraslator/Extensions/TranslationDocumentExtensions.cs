namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Extensions;

internal static class TranslationDocumentExtensions
{
    /// <summary>
    /// Builds a map from entity logical name to its plural (collection) display name
    /// for the given language code by scanning the "Entities" dataset.
    /// </summary>
    public static Dictionary<string, string> BuildEntityPluralNameMap(
        this TranslationWorkbookData data,
        string languageCode)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentException.ThrowIfNullOrWhiteSpace(languageCode);

        var pluralByEntity = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!data.Datasets.TryGetValue("Entities", out var entityRecords))
        {
            return pluralByEntity;
        }

        foreach (var record in entityRecords)
        {
            if (!string.Equals(record.TargetLcid, languageCode, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(record.TargetText))
            {
                continue;
            }

            var metadata = DeserializeMetadata(record.MetadataJson);

            if (!metadata.TryGetValue("Type", out var type) ||
                !type.Equals("DisplayCollectionName", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!metadata.TryGetValue("Entity Logical Name", out var entityLogicalName) ||
                string.IsNullOrWhiteSpace(entityLogicalName))
            {
                continue;
            }

            pluralByEntity[entityLogicalName] = record.TargetText;
        }

        return pluralByEntity;
    }

    private static Dictionary<string, string> DeserializeMetadata(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(metadataJson);
            return parsed is null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(parsed, StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
