namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Models.TranslationPipeline;

internal sealed class TranslationCacheStats
{
    public int Hits { get; init; }

    public int Misses { get; init; }

    public int TotalLookups => Hits + Misses;
}
