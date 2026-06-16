using Microsoft.Extensions.Options;

namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services;

internal sealed class PromptTemplateService : IPromptTemplateService
{
    private readonly string resourceSuffix;
    private readonly Lazy<string> cachedTemplate;

    public PromptTemplateService(IOptions<TranslatorCliOptions> options)
    {
        resourceSuffix = options.Value.PromptTemplateResourceSuffix;
        cachedTemplate = new Lazy<string>(LoadTemplate);
    }

    public string GetTemplate() => cachedTemplate.Value;

    private string LoadTemplate()
    {
        var assembly = typeof(PromptTemplateService).Assembly;
        var resourceName = assembly
            .GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(resourceSuffix, StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
        {
            throw new InvalidOperationException($"Embedded prompt resource '{resourceSuffix}' not found.");
        }

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            throw new InvalidOperationException($"Unable to open embedded prompt resource '{resourceName}'.");
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}







