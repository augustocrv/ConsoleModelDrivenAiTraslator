namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Models.TranslationPipeline;

internal sealed class TranslationServiceContext
{
    public required TranslationTable Sheet { get; init; }

    /// <summary>Full workbook data for cross-dataset lookups (e.g., entity plural names for Views translation).</summary>
    public TranslationWorkbookData? Workbook { get; init; }

    public required Dictionary<string, int> ColumnIndexes { get; init; }

    public required int SourceColumn { get; init; }

    public required int TargetColumn { get; init; }

    public required string SourceLanguageCode { get; init; }

    public required string TargetLanguageCode { get; init; }

    public required bool Force { get; init; }

    public required ConsoleModelDrivenAiTraslatorConfig TranslatorConfig { get; init; }

    public Dictionary<int, string>? RowsToTranslate { get; init; }

    public AiConnection? Connection { get; init; }

    public string? AdditionalTranslationContext { get; init; }

    public required Dictionary<string, string> LanguageCodes { get; init; }

    public HashSet<string>? IncludedViewTypes { get; init; }

    public HashSet<string>? ExcludedViewTypes { get; init; }
}
