using Microsoft.Extensions.Options;

namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services.Config;

internal sealed class LanguageCatalogService : ILanguageCatalogService
{
    private readonly TranslatorCliOptions options;

    public LanguageCatalogService(IOptions<TranslatorCliOptions> options)
    {
        this.options = options.Value;
    }

    public Dictionary<string, string> LoadLanguageCodes()
    {
        var lcidJson = ReadEmbeddedResourceText(options.WindowsLcidResourceSuffix);
        var deserializeOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var lcidEntries = JsonSerializer.Deserialize<List<WindowsLcidEntry>>(lcidJson, deserializeOptions) ?? new List<WindowsLcidEntry>();
        var codes = lcidEntries
            .Where(entry => entry.Lcid > 0)
            .GroupBy(entry => entry.Lcid)
            .Select(group => group.First())
            .ToDictionary(
                entry => entry.Lcid.ToString(CultureInfo.InvariantCulture),
                entry => string.IsNullOrWhiteSpace(entry.EnglishName) ? entry.DisplayName : entry.EnglishName,
                StringComparer.OrdinalIgnoreCase);

        if (codes.Count == 0)
        {
            throw new InvalidOperationException($"No language codes found in embedded resource '{options.WindowsLcidResourceSuffix}'.");
        }

        return codes;
    }

    private string ReadEmbeddedResourceText(string resourceSuffix)
    {
        var assembly = typeof(LanguageCatalogService).Assembly;
        var resourceName = assembly
            .GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith(resourceSuffix, StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
        {
            throw new InvalidOperationException($"Embedded resource '{resourceSuffix}' not found.");
        }

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            throw new InvalidOperationException($"Unable to open embedded resource '{resourceName}'.");
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private sealed class WindowsLcidEntry
    {
        public int Lcid { get; set; }

        public string EnglishName { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;
    }
}
