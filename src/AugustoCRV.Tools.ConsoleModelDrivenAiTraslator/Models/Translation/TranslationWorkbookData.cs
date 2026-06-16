namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Models.Translation;

internal sealed record TranslationWorkbookData
{
    public string? SourceLcid { get; init; }

    public string? TargetLcid { get; init; }

    public IReadOnlyDictionary<string, IReadOnlyList<TranslationRecord>> Datasets { get; init; }
        = new Dictionary<string, IReadOnlyList<TranslationRecord>>(StringComparer.OrdinalIgnoreCase);
}
