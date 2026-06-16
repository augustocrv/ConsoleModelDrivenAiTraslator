namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Models;

/// <summary>
/// Provides configurable defaults for CLI execution and infrastructure services.
/// </summary>
/// <summary>Class description.</summary>
public sealed class TranslatorCliOptions
{
    /// <summary>
    /// Gets or sets the default number of rows translated per Azure OpenAI request.
    /// </summary>
    public int DefaultTranslationBatchSize { get; set; } = 30;

    /// <summary>
    /// Gets or sets the default number of records imported per Dataverse batch.
    /// </summary>
    public int DefaultImportBatchSize { get; set; } = 100;

    /// <summary>
    /// Gets or sets the relative path under AppData used for connection persistence.
    /// </summary>
    public string ConnectionsRelativePath { get; set; } = Path.Combine("AugustoCRV", "Tools", "ConsoleModelDrivenAiTraslator");

    /// <summary>
    /// Gets or sets an optional explicit absolute path for connection persistence.
    /// </summary>
    public string? ConnectionsRootDirectory { get; set; }

    /// <summary>
    /// Gets or sets the embedded resource suffix used to locate the prompt template.
    /// </summary>
    public string PromptTemplateResourceSuffix { get; set; } = "Resources.PromptTemplate.md";

    /// <summary>
    /// Gets or sets the embedded resource suffix used to locate the default translator config JSON.
    /// </summary>
    public string DefaultConsoleModelDrivenAiTraslatorConfigResourceSuffix { get; set; } = "Resources.DefaultConsoleModelDrivenAiTraslatorConfig.json";

    /// <summary>
    /// Gets or sets the embedded resource suffix used to locate the Windows LCID catalog JSON.
    /// </summary>
    public string WindowsLcidResourceSuffix { get; set; } = "Resources.WindowsLcid.json";

    /// <summary>
    /// Gets or sets the timeout, in minutes, for Azure OpenAI requests.
    /// </summary>
    public int AzureOpenAiTimeoutMinutes { get; set; } = 5;

    /// <summary>
    /// Gets or sets the default model used for GitHub Copilot translation sessions.
    /// </summary>
    public string GitHubCopilotModel { get; set; } = "gpt-4.1";
}

