using Microsoft.Extensions.Options;

namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services;

internal sealed class DataverseConnectionSelectionService : IDataverseConnectionSelectionService
{
    private const string SelectionFileName = "selected-dataverse-connection.json";

    private readonly string selectionFilePath;
    private readonly IDataverseConnectionStoreService dataverseConnectionStore;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public DataverseConnectionSelectionService(
        IOptions<TranslatorCliOptions> options,
        IDataverseConnectionStoreService dataverseConnectionStore)
    {
        this.dataverseConnectionStore = dataverseConnectionStore;

        var configuredRoot = options.Value.ConnectionsRootDirectory;
        var baseFolder = string.IsNullOrWhiteSpace(configuredRoot)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), options.Value.ConnectionsRelativePath)
            : configuredRoot;

        Directory.CreateDirectory(baseFolder);
        selectionFilePath = Path.Combine(baseFolder, SelectionFileName);
    }

    public async Task<DataverseConnection?> GetSelectedConnectionAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(selectionFilePath))
        {
            return null;
        }

        var raw = await File.ReadAllTextAsync(selectionFilePath, cancellationToken).ConfigureAwait(false);
        var selected = JsonSerializer.Deserialize<SelectedDataverseConnection>(raw, JsonOptions);
        if (selected == null || string.IsNullOrWhiteSpace(selected.Name))
        {
            return null;
        }

        var connections = await dataverseConnectionStore.LoadAsync().ConfigureAwait(false);
        return connections.FirstOrDefault(c => c.Name.Equals(selected.Name, StringComparison.OrdinalIgnoreCase));
    }

    public async Task SetSelectedConnectionAsync(string connectionName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionName);

        var payload = new SelectedDataverseConnection { Name = connectionName };
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        await File.WriteAllTextAsync(selectionFilePath, json, cancellationToken).ConfigureAwait(false);
    }

    private sealed class SelectedDataverseConnection
    {
        public string Name { get; set; } = string.Empty;
    }
}
