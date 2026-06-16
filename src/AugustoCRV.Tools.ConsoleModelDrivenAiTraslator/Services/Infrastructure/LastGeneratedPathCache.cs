namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services.Infrastructure;

/// <summary>
/// Persists and retrieves the path of the last generated translated workbook
/// so that the push command can propose it as a default.
/// </summary>
internal interface ILastGeneratedPathCache
{
    Task<string?> GetAsync(CancellationToken cancellationToken = default);
    Task SetAsync(string path, CancellationToken cancellationToken = default);
}

internal sealed class LastGeneratedPathCache : ILastGeneratedPathCache
{
    private const string FileName = "last-generated-path.json";
    private static readonly SemaphoreSlim Lock = new(1, 1);
    private readonly string filePath;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public LastGeneratedPathCache()
    {
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AugustoCRV", "Tools", "ConsoleModelDrivenAiTraslator");
        Directory.CreateDirectory(appData);
        filePath = Path.Combine(appData, FileName);
    }

    public async Task<string?> GetAsync(CancellationToken cancellationToken = default)
    {
        await Lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            var json = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
            var entry = JsonSerializer.Deserialize<CacheEntry>(json, JsonOptions);
            return string.IsNullOrWhiteSpace(entry?.Path) ? null : entry.Path;
        }
        catch
        {
            return null;
        }
        finally
        {
            Lock.Release();
        }
    }

    public async Task SetAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        await Lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var json = JsonSerializer.Serialize(new CacheEntry { Path = path }, JsonOptions);
            await File.WriteAllTextAsync(filePath, json, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Lock.Release();
        }
    }

    private sealed class CacheEntry
    {
        public string Path { get; set; } = string.Empty;
    }
}
