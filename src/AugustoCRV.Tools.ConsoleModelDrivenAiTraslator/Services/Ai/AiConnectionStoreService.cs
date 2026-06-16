using Microsoft.Extensions.Options;

namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services
{
    /// <summary>Class description.</summary>
    public sealed class AiConnectionStoreService : IAiConnectionStoreService
    {
        private readonly string filePath;
        private readonly IApiKeyProtectorService apiKeyProtector;
        private readonly SemaphoreSlim cacheLock = new(1, 1);
        private List<AiConnection>? cachedConnections;
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        public AiConnectionStoreService(IOptions<TranslatorCliOptions> options, IApiKeyProtectorService apiKeyProtector)
        {
            this.apiKeyProtector = apiKeyProtector;

            var configuredRoot = options.Value.ConnectionsRootDirectory;
            var baseFolder = string.IsNullOrWhiteSpace(configuredRoot)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), options.Value.ConnectionsRelativePath)
                : configuredRoot;

            Directory.CreateDirectory(baseFolder);
            filePath = Path.Combine(baseFolder, "connections.json");
        }

        public async Task<List<AiConnection>> LoadAsync()
        {
            await cacheLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (cachedConnections != null)
                {
                    return cachedConnections.Select(CloneConnection).ToList();
                }

                if (!File.Exists(filePath))
                {
                    cachedConnections = new List<AiConnection>();
                    return new List<AiConnection>();
                }

                var json = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);
                var stored = JsonSerializer.Deserialize<List<AiConnection>>(json, JsonOptions) ?? new List<AiConnection>();
                cachedConnections = stored.Select(c => c.DecryptApiKey(apiKeyProtector)).ToList();

                return cachedConnections.Select(CloneConnection).ToList();
            }
            finally
            {
                cacheLock.Release();
            }
        }

        public async Task SaveAsync(List<AiConnection> connections)
        {
            ArgumentNullException.ThrowIfNull(connections);

            await cacheLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var toPersist = connections.Select(c => c.EncryptApiKey(apiKeyProtector)).ToList();
                var json = JsonSerializer.Serialize(toPersist, JsonOptions);
                await File.WriteAllTextAsync(filePath, json).ConfigureAwait(false);

                cachedConnections = connections.Select(CloneConnection).ToList();
            }
            finally
            {
                cacheLock.Release();
            }
        }

        private static AiConnection CloneConnection(AiConnection source)
        {
            return new AiConnection
            {
                Name = source.Name,
                Type = source.Type,
                DeploymentEndpoint = source.DeploymentEndpoint,
                ApiKey = source.ApiKey,
                Model = source.Model,
                Description = source.Description,
                LastValidatedUtc = source.LastValidatedUtc
            };
        }
    }
}



