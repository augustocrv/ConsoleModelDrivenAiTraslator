using Microsoft.Extensions.Options;

namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services;

internal sealed class AiConnectionSelectionService : IAiConnectionSelectionService
{
    private const string SelectionFileName = "selected-ai-connection.json";

    private readonly string selectionFilePath;
    private readonly IAiConnectionStoreService aiConnectionStore;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public AiConnectionSelectionService(
        IOptions<TranslatorCliOptions> options,
        IAiConnectionStoreService aiConnectionStore)
    {
        this.aiConnectionStore = aiConnectionStore;

        var configuredRoot = options.Value.ConnectionsRootDirectory;
        var baseFolder = string.IsNullOrWhiteSpace(configuredRoot)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), options.Value.ConnectionsRelativePath)
            : configuredRoot;

        Directory.CreateDirectory(baseFolder);
        selectionFilePath = Path.Combine(baseFolder, SelectionFileName);
    }

    public async Task<AiConnection?> GetSelectedConnectionAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(selectionFilePath))
        {
            return null;
        }

        var raw = await File.ReadAllTextAsync(selectionFilePath, cancellationToken).ConfigureAwait(false);
        var selected = JsonSerializer.Deserialize<SelectedAiConnection>(raw, JsonOptions);
        if (selected == null || string.IsNullOrWhiteSpace(selected.Name))
        {
            return null;
        }

        var connections = await aiConnectionStore.LoadAsync().ConfigureAwait(false);
        return connections.FirstOrDefault(c => c.Name.Equals(selected.Name, StringComparison.OrdinalIgnoreCase));
    }

    public async Task SetSelectedConnectionAsync(string connectionName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionName);

        var payload = new SelectedAiConnection { Name = connectionName };
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        await File.WriteAllTextAsync(selectionFilePath, json, cancellationToken).ConfigureAwait(false);
    }

    private sealed class SelectedAiConnection
    {
        public string Name { get; set; } = string.Empty;
    }
}
