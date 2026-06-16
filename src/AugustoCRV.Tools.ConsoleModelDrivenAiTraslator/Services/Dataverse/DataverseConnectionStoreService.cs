using Microsoft.Extensions.Options;

namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services;

/// <summary>
/// Service for persisting Dataverse connection configurations.
/// No encryption needed - OAuth tokens are cached separately.
/// </summary>
/// <summary>Class description.</summary>
public sealed class DataverseConnectionStoreService : IDataverseConnectionStoreService
{
    private readonly string filePath;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public DataverseConnectionStoreService(IOptions<TranslatorCliOptions> options)
    {
        var configuredRoot = options.Value.ConnectionsRootDirectory;
        var baseFolder = string.IsNullOrWhiteSpace(configuredRoot)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), options.Value.ConnectionsRelativePath)
            : configuredRoot;

        Directory.CreateDirectory(baseFolder);
        filePath = Path.Combine(baseFolder, "dataverse-connections.json");
    }

    public async Task<List<DataverseConnection>> LoadAsync()
    {
        if (!File.Exists(filePath))
        {
            return new List<DataverseConnection>();
        }

        var json = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);
        var stored = JsonSerializer.Deserialize<List<DataverseConnection>>(json, JsonOptions) ?? new List<DataverseConnection>();
        return stored;
    }

    public async Task SaveAsync(List<DataverseConnection> connections)
    {
        var json = JsonSerializer.Serialize(connections, JsonOptions);
        await File.WriteAllTextAsync(filePath, json).ConfigureAwait(false);
    }
}


