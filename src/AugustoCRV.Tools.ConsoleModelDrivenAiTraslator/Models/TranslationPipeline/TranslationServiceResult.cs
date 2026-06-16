namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Models.TranslationPipeline;

internal sealed class TranslationServiceResult
{
    public Dictionary<int, string> PendingRows { get; init; } = new();

    public Dictionary<int, string> TranslatedRows { get; init; } = new();

    public int StaticTranslationsCount { get; init; }
}
