namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services.Translation;

internal static class TranslationRecordImportHelper
{
    internal static Dictionary<string, string> DeserializeMetadata(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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

    internal static bool IsChecksumUnchanged(string? checksum, string? targetText)
    {
        if (string.IsNullOrWhiteSpace(checksum) || string.IsNullOrWhiteSpace(targetText))
            return false;
        var currentHash = SHA256.HashData(Encoding.UTF8.GetBytes(targetText));
        var currentChecksum = Convert.ToHexString(currentHash).ToLowerInvariant();
        return string.Equals(checksum, currentChecksum, StringComparison.OrdinalIgnoreCase);
    }

    internal static void ExecuteInBatches(IOrganizationService service, List<OrganizationRequest> requests)
    {
        if (requests.Count == 0)
            return;

        var batchSize = BaseRecordExporter.BulkCount > 0 ? BaseRecordExporter.BulkCount : 10;
        for (var i = 0; i < requests.Count; i += batchSize)
        {
            var batch = requests.Skip(i).Take(batchSize).ToList();
            var executeMultiple = new ExecuteMultipleRequest
            {
                Requests = new OrganizationRequestCollection(),
                Settings = new ExecuteMultipleSettings
                {
                    ContinueOnError = true,
                    ReturnResponses = false
                }
            };

            foreach (var request in batch)
            {
                executeMultiple.Requests.Add(request);
            }

            service.Execute(executeMultiple);
        }
    }
}
