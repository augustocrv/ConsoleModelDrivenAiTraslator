using AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Models.TranslationPipeline;

namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services.TranslationPipeline;

internal sealed class StaticTranslationService : ITranslationService
{
    public Task<TranslationServiceResult> ExecuteAsync(TranslationServiceContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var toTranslate = new Dictionary<int, string>();
        var translatedUsingConstants = 0;

        var sourceEntityPluralNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var targetEntityPluralNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (context.Sheet.Name == "Views" && context.Workbook is not null)
        {
            sourceEntityPluralNames = context.Workbook.BuildEntityPluralNameMap(context.SourceLanguageCode);
            targetEntityPluralNames = context.Workbook.BuildEntityPluralNameMap(context.TargetLanguageCode);
        }

        var hasViewTypeFilters = context.IncludedViewTypes is not null || context.ExcludedViewTypes is not null;
        var hasViewTypeColumn = context.ColumnIndexes.TryGetValue("ViewType", out var viewTypeCol);
        var sheetDimension = context.Sheet.Dimension;
        if (sheetDimension is null)
        {
            return Task.FromResult(new TranslationServiceResult
            {
                PendingRows = toTranslate,
                StaticTranslationsCount = translatedUsingConstants
            });
        }

        for (var row = 2; row <= sheetDimension.End.Row; row++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (context.Sheet.Name == "Views" && hasViewTypeFilters && hasViewTypeColumn)
            {
                var viewType = context.Sheet.Cells[row, viewTypeCol].Text.Trim();
                if (!IsViewTypeAllowed(viewType, context.IncludedViewTypes, context.ExcludedViewTypes))
                {
                    continue;
                }
            }

            var sourceText = context.Sheet.Cells[row, context.SourceColumn].Text;
            var targetText = context.Sheet.Cells[row, context.TargetColumn].Text;
            if (string.IsNullOrWhiteSpace(sourceText))
            {
                continue;
            }

            if (TryApplyFieldLogicalNameTranslation(context, row, out var fieldTranslation))
            {
                context.Sheet.Cells[row, context.TargetColumn].Value = fieldTranslation;
                translatedUsingConstants++;
                continue;
            }

            if (context.Sheet.Name == "Views" && TryApplyViewConstantTranslation(
                    context,
                    row,
                    sourceText,
                    sourceEntityPluralNames,
                    targetEntityPluralNames))
            {
                translatedUsingConstants++;
                continue;
            }

            if (context.Force || string.IsNullOrWhiteSpace(targetText))
            {
                toTranslate[row] = sourceText;
            }
        }

        return Task.FromResult(new TranslationServiceResult
        {
            PendingRows = toTranslate,
            StaticTranslationsCount = translatedUsingConstants
        });
    }

    private static bool IsViewTypeAllowed(
        string viewType,
        HashSet<string>? includedViewTypes,
        HashSet<string>? excludedViewTypes)
    {
        if (includedViewTypes is not null && includedViewTypes.Count > 0 && !includedViewTypes.Contains(viewType))
        {
            return false;
        }

        if (excludedViewTypes is not null && excludedViewTypes.Count > 0 && excludedViewTypes.Contains(viewType))
        {
            return false;
        }

        return true;
    }

    private static bool TryApplyViewConstantTranslation(
        TranslationServiceContext context,
        int row,
        string sourceText,
        Dictionary<string, string> sourceEntityPluralNames,
        Dictionary<string, string> targetEntityPluralNames)
    {
        var viewConfig = context.TranslatorConfig.ViewTranslation;
        if (viewConfig == null)
        {
            return false;
        }

        if (!context.ColumnIndexes.TryGetValue("Entity Logical Name", out var entityLogicalNameCol) ||
            !context.ColumnIndexes.TryGetValue("ViewType", out var viewTypeCol) ||
            !context.ColumnIndexes.TryGetValue("Type", out var typeCol))
        {
            return false;
        }

        var rowType = context.Sheet.Cells[row, typeCol].Text.Trim();
        if (!rowType.Equals("Name", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (viewConfig.ExactTranslations.TryGetNestedValueIgnoreCase(context.TargetLanguageCode, out var exactTranslations) &&
            exactTranslations.TryGetValueIgnoreCase(sourceText, out var exactTranslation) &&
            !string.IsNullOrWhiteSpace(exactTranslation))
        {
            context.Sheet.Cells[row, context.TargetColumn].Value = exactTranslation;
            return true;
        }

        var entityLogicalName = context.Sheet.Cells[row, entityLogicalNameCol].Text.Trim();
        if (string.IsNullOrWhiteSpace(entityLogicalName) ||
            !targetEntityPluralNames.TryGetValue(entityLogicalName, out var targetEntityPluralName) ||
            string.IsNullOrWhiteSpace(targetEntityPluralName))
        {
            return false;
        }

        var viewType = context.Sheet.Cells[row, viewTypeCol].Text.Trim();
        var ruleKey = viewConfig.ResolveViewRuleKey(
            sourceText,
            viewType,
            context.SourceLanguageCode,
            entityLogicalName,
            sourceEntityPluralNames);

        if (string.IsNullOrWhiteSpace(ruleKey) ||
            !viewConfig.Templates.TryGetNestedValueIgnoreCase(context.TargetLanguageCode, out var templatesForLanguage) ||
            !templatesForLanguage.TryGetValueIgnoreCase(ruleKey, out var templateConfig) ||
            templateConfig == null)
        {
            return false;
        }

        if (viewConfig.IsPlaceholderTemplateForLanguage(ruleKey, context.TargetLanguageCode, templateConfig))
        {
            return false;
        }

        var resolvedTemplate = viewConfig.ResolveTemplateForEntity(templateConfig, context.TargetLanguageCode, entityLogicalName);
        if (string.IsNullOrWhiteSpace(resolvedTemplate))
        {
            return false;
        }

        var translated = resolvedTemplate.Replace("{entityPlural}", targetEntityPluralName, StringComparison.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(translated))
        {
            return false;
        }

        context.Sheet.Cells[row, context.TargetColumn].Value = translated;
        return true;
    }

    private static bool TryApplyFieldLogicalNameTranslation(
        TranslationServiceContext context,
        int row,
        out string translated)
    {
        translated = string.Empty;

        var sheetName = context.Sheet.Name;
        var isAttributes = sheetName.Equals("Attributes", StringComparison.OrdinalIgnoreCase);
        var isFormsFields = sheetName.Equals("Forms Fields", StringComparison.OrdinalIgnoreCase);
        if (!isAttributes && !isFormsFields)
        {
            return false;
        }

        var fieldConfig = context.TranslatorConfig.FieldTranslation;
        if (!fieldConfig.LogicalNameTranslations.TryGetNestedValueIgnoreCase(context.TargetLanguageCode, out var translationsByLanguage) ||
            translationsByLanguage.Count == 0)
        {
            return false;
        }

        var logicalNameColumn = isAttributes ? "Attribute Logical Name" : "Attribute";
        if (!context.ColumnIndexes.TryGetValue(logicalNameColumn, out var logicalNameCol))
        {
            return false;
        }

        if (isAttributes && context.ColumnIndexes.TryGetValue("Type", out var typeCol))
        {
            var rowType = context.Sheet.Cells[row, typeCol].Text.Trim();
            if (!rowType.Equals("DisplayName", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        var logicalName = context.Sheet.Cells[row, logicalNameCol].Text.Trim();
        if (string.IsNullOrWhiteSpace(logicalName))
        {
            return false;
        }

        if (translationsByLanguage.TryGetValueIgnoreCase(logicalName, out var exactTranslation) &&
            !string.IsNullOrWhiteSpace(exactTranslation))
        {
            translated = exactTranslation;
            return true;
        }

        if (translationsByLanguage.TryGetValueIgnoreCase("*", out var template) &&
            !string.IsNullOrWhiteSpace(template))
        {
            translated = template.Replace("{logicalName}", logicalName, StringComparison.OrdinalIgnoreCase);
            return !string.IsNullOrWhiteSpace(translated);
        }

        return false;
    }
}
