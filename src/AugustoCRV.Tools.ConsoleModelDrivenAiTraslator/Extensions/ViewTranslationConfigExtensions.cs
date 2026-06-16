using System.Text.RegularExpressions;

namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Extensions;

internal static class ViewTranslationConfigExtensions
{
    public static string ResolveViewRuleKey(
        this ViewTranslationConfig viewConfig,
        string sourceText,
        string viewType,
        string sourceLanguageCode,
        string entityLogicalName,
        Dictionary<string, string> sourceEntityPluralNames)
    {
        if (viewConfig.ViewTypeRuleMap.TryGetValueIgnoreCase(viewType, out var ruleFromViewType) &&
            !string.IsNullOrWhiteSpace(ruleFromViewType))
        {
            return ruleFromViewType;
        }

        if (!sourceEntityPluralNames.TryGetValue(entityLogicalName, out var sourcePluralName) ||
            string.IsNullOrWhiteSpace(sourcePluralName))
        {
            return string.Empty;
        }

        if (!viewConfig.SourcePatterns.TryGetNestedValueIgnoreCase(sourceLanguageCode, out var patternsByRule) ||
            patternsByRule.Count == 0)
        {
            return string.Empty;
        }

        foreach (var pattern in patternsByRule)
        {
            if (string.IsNullOrWhiteSpace(pattern.Value))
            {
                continue;
            }

            var finalPattern = pattern.Value.Replace("{entityPlural}", Regex.Escape(sourcePluralName), StringComparison.OrdinalIgnoreCase);
            if (Regex.IsMatch(sourceText, finalPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250)))
            {
                return pattern.Key;
            }
        }

        return string.Empty;
    }

    public static string ResolveTemplateForEntity(
        this ViewTranslationConfig viewConfig,
        ViewTranslationTemplateConfig templateConfig,
        string targetLanguageCode,
        string entityLogicalName)
    {
        var fallback = templateConfig.Default ?? string.Empty;
        if (!viewConfig.EntityGenderByLanguage.TryGetNestedValueIgnoreCase(targetLanguageCode, out var entityGenderMap) ||
            !entityGenderMap.TryGetValueIgnoreCase(entityLogicalName, out var gender) ||
            string.IsNullOrWhiteSpace(gender))
        {
            return fallback;
        }

        if (!templateConfig.Gender.TryGetValueIgnoreCase(gender, out var genderTemplate) ||
            string.IsNullOrWhiteSpace(genderTemplate))
        {
            return fallback;
        }

        return genderTemplate;
    }

    public static bool IsPlaceholderTemplateForLanguage(
        this ViewTranslationConfig viewConfig,
        string ruleKey,
        string targetLanguageCode,
        ViewTranslationTemplateConfig targetTemplate)
    {
        if (targetLanguageCode.Equals("1033", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!viewConfig.Templates.TryGetNestedValueIgnoreCase("1033", out var englishTemplates) ||
            !englishTemplates.TryGetValueIgnoreCase(ruleKey, out var englishTemplate))
        {
            return false;
        }

        var hasTargetExactTranslations =
            viewConfig.ExactTranslations.TryGetNestedValueIgnoreCase(targetLanguageCode, out var exactByLanguage) &&
            exactByLanguage.Count > 0;

        var hasTargetGenderMap =
            viewConfig.EntityGenderByLanguage.TryGetNestedValueIgnoreCase(targetLanguageCode, out var genderByLanguage) &&
            genderByLanguage.Count > 0;

        var hasTemplateGenderRules = targetTemplate.Gender.Count > 0;

        if (hasTargetExactTranslations || hasTargetGenderMap || hasTemplateGenderRules)
        {
            return false;
        }

        return string.Equals(targetTemplate.Default, englishTemplate.Default, StringComparison.Ordinal);
    }
}
