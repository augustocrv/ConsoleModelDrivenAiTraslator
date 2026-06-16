using AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Models.TranslationPipeline;

namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services.TranslationPipeline;

internal sealed class TranslationCacheService : ITranslationCacheService
{
    private readonly Dictionary<string, Dictionary<string, string>> translationCache = new();
    private int cacheHits;
    private int cacheMisses;

    public void Reset()
    {
        translationCache.Clear();
        cacheHits = 0;
        cacheMisses = 0;
    }

    public (Dictionary<int, string> cachedItems, Dictionary<int, string> remainingItems) ApplyCached(
        Dictionary<int, string> items,
        string targetLanguageCode,
        TranslationTable sheet,
        int targetCol)
    {
        var cachedItems = new Dictionary<int, string>();
        var remainingItems = new Dictionary<int, string>();

        foreach (var item in items)
        {
            var cachedTranslation = GetCachedTranslation(item.Value, targetLanguageCode);
            if (cachedTranslation != null)
            {
                cachedItems[item.Key] = cachedTranslation;
                sheet.Cells[item.Key, targetCol].Value = cachedTranslation;
            }
            else
            {
                remainingItems[item.Key] = item.Value;
            }
        }

        return (cachedItems, remainingItems);
    }

    public void StoreTranslations(
        Dictionary<int, string> originalTexts,
        Dictionary<int, string> translations,
        string targetLanguageCode)
    {
        translationCache.TryAdd(targetLanguageCode, new Dictionary<string, string>());

        foreach (var translation in translations)
        {
            if (!originalTexts.TryGetValue(translation.Key, out var originalText))
            {
                continue;
            }

            var normalizedText = NormalizeForCache(originalText);
            translationCache[targetLanguageCode][normalizedText] = translation.Value;
        }
    }

    public TranslationCacheStats GetStats()
    {
        return new TranslationCacheStats
        {
            Hits = cacheHits,
            Misses = cacheMisses
        };
    }

    private string? GetCachedTranslation(string sourceText, string targetLanguageCode)
    {
        var normalizedText = NormalizeForCache(sourceText);
        if (translationCache.TryGetValue(targetLanguageCode, out var langCache) &&
            langCache.TryGetValue(normalizedText, out var cachedValue))
        {
            cacheHits++;
            return cachedValue;
        }

        cacheMisses++;
        return null;
    }

    private static string NormalizeForCache(string text)
    {
        return text?.Trim().ToLowerInvariant() ?? string.Empty;
    }
}

