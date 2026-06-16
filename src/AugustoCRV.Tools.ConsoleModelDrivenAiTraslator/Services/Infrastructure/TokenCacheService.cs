using AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Models.Dataverse;
using System.Text.Json;

namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services.Infrastructure;

/// <summary>
/// Service for managing OAuth token cache for Dataverse connections.
/// </summary>
internal interface ITokenCacheService
{
    /// <summary>
    /// Attempts to retrieve a cached token for the specified connection.
    /// </summary>
    Task<TokenDefinition?> TryGetTokenAsync(string connectionName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves an access token to the cache.
    /// </summary>
    Task SaveTokenAsync(string connectionName, Uri serviceUri, string accessToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a token from the cache.
    /// </summary>
    Task ClearTokenAsync(string connectionName, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of token cache service that persists tokens to disk.
/// </summary>
internal sealed class TokenCacheService : ITokenCacheService
{
    private const string CacheFileName = "dataverse-tokens.json";
    private static readonly SemaphoreSlim Lock = new(1, 1);
    private readonly string cacheFilePath;

    public TokenCacheService()
    {
        var folderPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appDataPath = Path.Combine(folderPath, "AugustoCRV", "Tools", "ConsoleModelDrivenAiTraslator");
        Directory.CreateDirectory(appDataPath);

        cacheFilePath = Path.Combine(appDataPath, CacheFileName);
    }

    public async Task<TokenDefinition?> TryGetTokenAsync(string connectionName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionName);

        await Lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var cache = await LoadCacheAsync(cancellationToken).ConfigureAwait(false);
            if (!cache.TryGetValue(connectionName, out var token))
            {
                return null;
            }

            return token?.IsValid == true ? token : null;
        }
        finally
        {
            Lock.Release();
        }
    }

    public async Task SaveTokenAsync(string connectionName, Uri serviceUri, string accessToken, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionName);
        ArgumentNullException.ThrowIfNull(serviceUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        await Lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var cache = await LoadCacheAsync(cancellationToken).ConfigureAwait(false);
            cache[connectionName] = new TokenDefinition(serviceUri, accessToken);
            await SaveCacheAsync(cache, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Lock.Release();
        }
    }

    public async Task ClearTokenAsync(string connectionName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionName);

        await Lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var cache = await LoadCacheAsync(cancellationToken).ConfigureAwait(false);
            if (cache.Remove(connectionName))
            {
                await SaveCacheAsync(cache, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            Lock.Release();
        }
    }

    private async Task<Dictionary<string, TokenDefinition>> LoadCacheAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(cacheFilePath))
        {
            return new Dictionary<string, TokenDefinition>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var json = await File.ReadAllTextAsync(cacheFilePath, cancellationToken).ConfigureAwait(false);
            var cache = JsonSerializer.Deserialize<Dictionary<string, TokenDefinition>>(json);
            return cache ?? new Dictionary<string, TokenDefinition>(StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return new Dictionary<string, TokenDefinition>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private async Task SaveCacheAsync(Dictionary<string, TokenDefinition> cache, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(cache, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        await File.WriteAllTextAsync(cacheFilePath, json, cancellationToken).ConfigureAwait(false);
    }
}
