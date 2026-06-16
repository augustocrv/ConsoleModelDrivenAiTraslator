using AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Models.TranslationPipeline;

namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services.TranslationPipeline;

internal interface ITranslationCacheService
{
    void Reset();

    (Dictionary<int, string> cachedItems, Dictionary<int, string> remainingItems) ApplyCached(
        Dictionary<int, string> items,
        string targetLanguageCode,
        TranslationTable sheet,
        int targetCol);

    void StoreTranslations(
        Dictionary<int, string> originalTexts,
        Dictionary<int, string> translations,
        string targetLanguageCode);

    TranslationCacheStats GetStats();
}

