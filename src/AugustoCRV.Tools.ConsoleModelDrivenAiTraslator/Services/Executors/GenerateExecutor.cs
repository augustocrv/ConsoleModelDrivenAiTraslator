using Microsoft.Extensions.Options;
using AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services.Csv;
using AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services.Infrastructure;
using AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Models.TranslationPipeline;
using AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services.Config;
using AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services.Translation;
using AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services.TranslationPipeline;

namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services;

internal sealed class GenerateExecutor : IGenerateExecutor
{
    private const string DefaultConfigFileName = "ConsoleModelDrivenAiTraslator.config.json";

    private readonly IAiConnectionSelectionService aiConnectionSelectionService;
    private readonly IDataverseConnectionSelectionService dataverseConnectionSelectionService;
    private readonly IExportOriginalExecutor exportOriginalExecutor;
    private readonly KeyedServiceFactory keyedServiceFactory;
    private readonly ConsoleModelDrivenAiTraslatorConfigJsonService ConsoleModelDrivenAiTraslatorConfigJsonService;
    private readonly ITranslationCacheService translationCacheService;
    private readonly ITranslationWorkbookStorage workbookStorage;
    private readonly ILastGeneratedPathCache lastGeneratedPathCache;
    private readonly TranslatorCliOptions options;
    private readonly Dictionary<string, string> languageCodes;

    private int batchSize;
    private ConsoleModelDrivenAiTraslatorConfig translatorConfig = new();

    public GenerateExecutor(
        IAiConnectionSelectionService aiConnectionSelectionService,
        IDataverseConnectionSelectionService dataverseConnectionSelectionService,
        IExportOriginalExecutor exportOriginalExecutor,
        KeyedServiceFactory keyedServiceFactory,
        ConsoleModelDrivenAiTraslatorConfigJsonService ConsoleModelDrivenAiTraslatorConfigJsonService,
        ILanguageCatalogService languageCatalogService,
        ITranslationCacheService translationCacheService,
        ITranslationWorkbookStorage workbookStorage,
        ILastGeneratedPathCache lastGeneratedPathCache,
        IOptions<TranslatorCliOptions> options)
    {
        this.aiConnectionSelectionService = aiConnectionSelectionService;
        this.dataverseConnectionSelectionService = dataverseConnectionSelectionService;
        this.exportOriginalExecutor = exportOriginalExecutor;
        this.keyedServiceFactory = keyedServiceFactory;
        this.ConsoleModelDrivenAiTraslatorConfigJsonService = ConsoleModelDrivenAiTraslatorConfigJsonService;
        this.translationCacheService = translationCacheService;
        this.workbookStorage = workbookStorage;
        this.lastGeneratedPathCache = lastGeneratedPathCache;
        this.options = options.Value;
        languageCodes = languageCatalogService.LoadLanguageCodes();
    }

    public async Task<int> ExecuteAsync(
        string solutionName,
        string sourceLanguageCode,
        string targetLanguageCodes,
        string? sourceCsvFile,
        string? translationContext,
        string? includeViewTypes,
        string? excludeViewTypes,
        string? exportFolder,
        bool enableManaged,
        bool force,
        CancellationToken cancellationToken)
    {
        batchSize = options.DefaultTranslationBatchSize;
        translatorConfig = new ConsoleModelDrivenAiTraslatorConfig();
        translationCacheService.Reset();

        try
        {
            Console.OutputEncoding = Encoding.UTF8;

            if (!ValidateParameters(sourceLanguageCode, targetLanguageCodes, includeViewTypes, excludeViewTypes))
            {
                return 1;
            }

            var selectedAiConnection = await aiConnectionSelectionService.GetSelectedConnectionAsync(cancellationToken).ConfigureAwait(false);
            if (selectedAiConnection is null)
            {
                AnsiConsole.Console.WriteError("No AI connection selected. Run 'conn ai select' first.");
                return 1;
            }

            var selectedDataverseConnection = await dataverseConnectionSelectionService.GetSelectedConnectionAsync(cancellationToken).ConfigureAwait(false);
            if (selectedDataverseConnection is null)
            {
                AnsiConsole.Console.WriteError("No Dataverse connection selected. Run 'conn dataverse select' first.");
                return 1;
            }

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            var outputDirectory = string.IsNullOrWhiteSpace(exportFolder)
                ? Directory.GetCurrentDirectory()
                : exportFolder;
            Directory.CreateDirectory(outputDirectory);

            TranslationWorkbookData workbookData;
            if (!string.IsNullOrWhiteSpace(sourceCsvFile) && (Directory.Exists(sourceCsvFile) || File.Exists(sourceCsvFile)))
            {
                AnsiConsole.Console.WriteInfo($"Using existing source CSV workbook: {sourceCsvFile}");
                workbookData = workbookStorage.Load(sourceCsvFile);
            }
            else
            {
                AnsiConsole.Console.WriteInfo("Source CSV workbook not found. Exporting from Dataverse...");
                workbookData = await exportOriginalExecutor.ExportToMemoryAsync(
                    solutionName,
                    sourceLanguageCode,
                    enableManaged,
                    cancellationToken).ConfigureAwait(false);
            }

            var targetCodes = targetLanguageCodes
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            await LoadConsoleModelDrivenAiTraslatorConfigurationAsync(targetCodes, cancellationToken).ConfigureAwait(false);

            var includeSet = ParseFilterSet(includeViewTypes);
            var excludeSet = ParseFilterSet(excludeViewTypes);

            var translatedPath = Path.Combine(outputDirectory, $"{solutionName}_{timestamp}_translated.csv");
            TranslationWorkbookData translatedWorkbook = workbookData;

            foreach (var targetLanguageCode in targetCodes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                translatedWorkbook = await TranslateWorkbookAsync(
                    translatedWorkbook,
                    sourceLanguageCode,
                    targetLanguageCode,
                    translationContext,
                    includeSet,
                    excludeSet,
                    force,
                    selectedAiConnection,
                    cancellationToken).ConfigureAwait(false);
            }

            var selectedTargets = targetCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var filteredWorkbook = FilterWorkbookForSelectedTargets(translatedWorkbook, sourceLanguageCode, selectedTargets);
            workbookStorage.Save(filteredWorkbook, translatedPath);

            await lastGeneratedPathCache.SetAsync(translatedPath, cancellationToken).ConfigureAwait(false);

            AnsiConsole.Console.WriteSuccess($"Translated workbook CSV generated: {translatedPath}");

            return 0;
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.Console.WriteWarning("Translation canceled.");
            return 130;
        }
        catch (Exception ex)
        {
            AnsiConsole.Console.WriteError($"Translation failed: {ex.Message}");
            return 1;
        }
    }

    private async Task<TranslationWorkbookData> TranslateWorkbookAsync(
        TranslationWorkbookData workbookData,
        string sourceLanguageCode,
        string targetLanguageCode,
        string? translationContext,
        HashSet<string>? includeViewTypes,
        HashSet<string>? excludeViewTypes,
        bool force,
        AiConnection connection,
        CancellationToken cancellationToken)
    {
        if (!workbookData.Datasets.Any())
        {
            throw new InvalidOperationException("Source workbook does not contain any datasets.");
        }

        var updatedDatasets = new Dictionary<string, IReadOnlyList<TranslationRecord>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (datasetName, allRecords) in workbookData.Datasets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (table, rowNumbers) = BuildTableForDataset(datasetName, allRecords, sourceLanguageCode, targetLanguageCode);

            if (table.Dimension is null)
            {
                updatedDatasets[datasetName] = allRecords;
                continue;
            }

            var columns = table.GetHeaderDictionary();
            if (!columns.TryGetValue(sourceLanguageCode, out var sourceCol))
            {
                updatedDatasets[datasetName] = allRecords;
                continue;
            }

            if (!columns.TryGetValue(targetLanguageCode, out var targetCol))
            {
                updatedDatasets[datasetName] = allRecords;
                continue;
            }

            var staticService = keyedServiceFactory.GetRequired<ITranslationService>(TranslationPipelineKind.Static);
            var staticResult = await staticService.ExecuteAsync(new TranslationServiceContext
            {
                Workbook = workbookData,
                Sheet = table,
                ColumnIndexes = columns,
                SourceColumn = sourceCol,
                TargetColumn = targetCol,
                SourceLanguageCode = sourceLanguageCode,
                TargetLanguageCode = targetLanguageCode,
                Force = force,
                TranslatorConfig = translatorConfig,
                LanguageCodes = languageCodes,
                IncludedViewTypes = includeViewTypes,
                ExcludedViewTypes = excludeViewTypes
            }, cancellationToken).ConfigureAwait(false);

            var toTranslate = staticResult.PendingRows;
            var (cachedTranslations, remainingToTranslate) = translationCacheService.ApplyCached(toTranslate, targetLanguageCode, table, targetCol);
            if (cachedTranslations.Count > 0)
            {
                AnsiConsole.Console.WriteInfo($"[{datasetName}] Cache hits: {cachedTranslations.Count}");
            }

            if (remainingToTranslate.Count > 0)
            {
                var batches = remainingToTranslate
                    .Select((entry, index) => new { entry, index })
                    .GroupBy(x => x.index / batchSize)
                    .Select(static group => group.ToDictionary(x => x.entry.Key, x => x.entry.Value))
                    .ToList();

                var aiService = keyedServiceFactory.GetRequired<ITranslationService>(TranslationPipelineKind.Ai);
                foreach (var batch in batches)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var (batchCached, batchRemaining) = translationCacheService.ApplyCached(batch, targetLanguageCode, table, targetCol);
                    if (batchCached.Count > 0)
                    {
                        AnsiConsole.Console.WriteInfo($"[{datasetName}] Batch cache hits: {batchCached.Count}");
                    }

                    if (batchRemaining.Count == 0)
                    {
                        continue;
                    }

                    var aiResult = await aiService.ExecuteAsync(new TranslationServiceContext
                    {
                        Sheet = table,
                        ColumnIndexes = columns,
                        SourceColumn = sourceCol,
                        TargetColumn = targetCol,
                        SourceLanguageCode = sourceLanguageCode,
                        TargetLanguageCode = targetLanguageCode,
                        Force = force,
                        TranslatorConfig = translatorConfig,
                        RowsToTranslate = batchRemaining,
                        Connection = connection,
                        AdditionalTranslationContext = translationContext,
                        LanguageCodes = languageCodes,
                        IncludedViewTypes = includeViewTypes,
                        ExcludedViewTypes = excludeViewTypes
                    }, cancellationToken).ConfigureAwait(false);

                    var translatedRows = aiResult.TranslatedRows;
                    if (translatedRows.Count == 0)
                    {
                        continue;
                    }

                    translationCacheService.StoreTranslations(batchRemaining, translatedRows, targetLanguageCode);

                    foreach (var row in translatedRows)
                    {
                        if (table.Dimension?.End.Row >= row.Key)
                        {
                            table.GetCell(row.Key, targetCol).Value = row.Value;
                        }
                    }
                }
            }

            updatedDatasets[datasetName] = MergeTranslatedRecords(
                allRecords, table, rowNumbers, sourceLanguageCode, targetLanguageCode, targetCol);
        }

        var translatedWorkbook = new TranslationWorkbookData
        {
            SourceLcid = sourceLanguageCode,
            TargetLcid = targetLanguageCode,
            Datasets = updatedDatasets
        };

        return translatedWorkbook;
    }

    private static TranslationWorkbookData FilterWorkbookForSelectedTargets(
        TranslationWorkbookData workbook,
        string sourceLanguageCode,
        IReadOnlySet<string> selectedTargetLcids)
    {
        var filteredDatasets = workbook.Datasets.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyList<TranslationRecord>)kvp.Value
                .Where(record =>
                    selectedTargetLcids.Contains(record.TargetLcid) &&
                    !string.Equals(record.SourceLcid, record.TargetLcid, StringComparison.OrdinalIgnoreCase))
                .OrderBy(record => record.RowNumber)
                .ToList(),
            StringComparer.OrdinalIgnoreCase);

        var firstTarget = selectedTargetLcids.FirstOrDefault();

        return new TranslationWorkbookData
        {
            SourceLcid = sourceLanguageCode,
            TargetLcid = firstTarget,
            Datasets = filteredDatasets
        };
    }

    /// <summary>
    /// Builds a TranslationTable for the pipeline from a dataset's records, scoped to the
    /// given source and target language codes. Also returns the RowNumbers for table rows 2, 3, ...
    /// </summary>
    private static (TranslationTable Table, List<int> RowNumbers) BuildTableForDataset(
        string datasetName,
        IReadOnlyList<TranslationRecord> allRecords,
        string sourceLanguageCode,
        string targetLanguageCode)
    {
        var table = new TranslationTable(datasetName);

        // Prefer records already for the target language; fall back to source-language records for new languages.
        var targetRecords = allRecords
            .Where(r => string.Equals(r.TargetLcid, targetLanguageCode, StringComparison.OrdinalIgnoreCase))
            .OrderBy(r => r.RowNumber)
            .ToList();

        var sourceRecords = targetRecords.Count > 0
            ? targetRecords
            : allRecords
                .Where(r => string.Equals(r.TargetLcid, sourceLanguageCode, StringComparison.OrdinalIgnoreCase))
                .OrderBy(r => r.RowNumber)
                .ToList();

        if (sourceRecords.Count == 0)
        {
            return (table, new List<int>());
        }

        var isNewLanguage = targetRecords.Count == 0;

        // Derive metadata column names from the first record.
        var metaCols = TranslationRecordImportHelper.DeserializeMetadata(sourceRecords[0].MetadataJson).Keys.ToList();

        // Build header: [meta cols...] [sourceLcid] [targetLcid] [Checksum_targetLcid]
        var col = 1;
        foreach (var metaCol in metaCols)
        {
            table.GetCell(1, col++).Value = metaCol;
        }

        var sourceColIdx = col++;
        table.GetCell(1, sourceColIdx).Value = sourceLanguageCode;

        var targetColIdx = col++;
        table.GetCell(1, targetColIdx).Value = targetLanguageCode;

        table.GetCell(1, col).Value = $"Checksum_{targetLanguageCode}";

        // Build data rows.
        var rowNumbers = new List<int>(sourceRecords.Count);
        var tableRow = 2;

        foreach (var record in sourceRecords)
        {
            var metadata = TranslationRecordImportHelper.DeserializeMetadata(record.MetadataJson);
            col = 1;
            foreach (var metaColName in metaCols)
            {
                table.GetCell(tableRow, col++).Value =
                    metadata.TryGetValue(metaColName, out var mv) ? mv : string.Empty;
            }

            table.GetCell(tableRow, col++).Value = record.SourceText;
            table.GetCell(tableRow, col++).Value = isNewLanguage ? string.Empty : record.TargetText;
            table.GetCell(tableRow, col).Value = isNewLanguage ? string.Empty : record.Checksum;

            rowNumbers.Add(record.RowNumber);
            tableRow++;
        }

        return (table, rowNumbers);
    }

    /// <summary>
    /// Merges translated cell values from the table back into the original records list.
    /// Creates new TranslationRecord entries when translating into a previously unseen language.
    /// </summary>
    private static IReadOnlyList<TranslationRecord> MergeTranslatedRecords(
        IReadOnlyList<TranslationRecord> allRecords,
        TranslationTable table,
        List<int> rowNumbers,
        string sourceLanguageCode,
        string targetLanguageCode,
        int targetCol)
    {
        if (rowNumbers.Count == 0)
        {
            return allRecords;
        }

        // Map RowNumber → 1-based table row (data rows start at 2).
        var rowNumberToTableRow = new Dictionary<int, int>(rowNumbers.Count);
        for (var i = 0; i < rowNumbers.Count; i++)
        {
            rowNumberToTableRow[rowNumbers[i]] = i + 2;
        }

        var hasExistingTargetRecords = allRecords.Any(r =>
            string.Equals(r.TargetLcid, targetLanguageCode, StringComparison.OrdinalIgnoreCase));

        if (!hasExistingTargetRecords)
        {
            // New language: append new records derived from source-language records.
            var result = new List<TranslationRecord>(allRecords);
            foreach (var sourceRecord in allRecords
                .Where(r => string.Equals(r.TargetLcid, sourceLanguageCode, StringComparison.OrdinalIgnoreCase))
                .OrderBy(r => r.RowNumber))
            {
                if (!rowNumberToTableRow.TryGetValue(sourceRecord.RowNumber, out var tableRow))
                {
                    continue;
                }

                var translatedText = table.GetCell(tableRow, targetCol).Value?.ToString() ?? string.Empty;
                var checksum = string.IsNullOrEmpty(translatedText) ? string.Empty : ComputeChecksum(translatedText);

                result.Add(sourceRecord with
                {
                    TargetLcid = targetLanguageCode,
                    TargetText = translatedText,
                    Checksum = checksum,
                    RecordKey = $"{sourceRecord.Dataset}|{sourceRecord.RowNumber}|{targetLanguageCode}"
                });
            }

            return result;
        }

        // Existing language: update records whose translation changed.
        var updated = new List<TranslationRecord>(allRecords.Count);
        foreach (var record in allRecords)
        {
            if (!string.Equals(record.TargetLcid, targetLanguageCode, StringComparison.OrdinalIgnoreCase))
            {
                updated.Add(record);
                continue;
            }

            if (!rowNumberToTableRow.TryGetValue(record.RowNumber, out var tableRow))
            {
                updated.Add(record);
                continue;
            }

            var translatedText = table.GetCell(tableRow, targetCol).Value?.ToString() ?? string.Empty;
            if (string.Equals(translatedText, record.TargetText, StringComparison.Ordinal))
            {
                updated.Add(record);
            }
            else
            {
                var checksum = string.IsNullOrEmpty(translatedText) ? string.Empty : ComputeChecksum(translatedText);
                updated.Add(record with { TargetText = translatedText, Checksum = checksum });
            }
        }

        return updated;
    }

    private bool ValidateParameters(
        string sourceLanguageCode,
        string targetLanguageCodes,
        string? includeViewTypes,
        string? excludeViewTypes)
    {
        if (!languageCodes.ContainsKey(sourceLanguageCode))
        {
            AnsiConsole.Console.WriteError($"Unsupported source language code '{sourceLanguageCode}'.");
            return false;
        }

        if (!string.IsNullOrWhiteSpace(includeViewTypes) && !string.IsNullOrWhiteSpace(excludeViewTypes))
        {
            AnsiConsole.Console.WriteError("Use either include or exclude view-type filters, not both.");
            return false;
        }

        var targetCodes = targetLanguageCodes
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (targetCodes.Count == 0)
        {
            AnsiConsole.Console.WriteError("No target language codes provided.");
            return false;
        }

        foreach (var targetCode in targetCodes)
        {
            if (!languageCodes.ContainsKey(targetCode))
            {
                AnsiConsole.Console.WriteError($"Unsupported target language code '{targetCode}'.");
                return false;
            }

            if (targetCode.Equals(sourceLanguageCode, StringComparison.OrdinalIgnoreCase))
            {
                AnsiConsole.Console.WriteError("Source and target language codes cannot be the same.");
                return false;
            }
        }

        return true;
    }

    private async Task LoadConsoleModelDrivenAiTraslatorConfigurationAsync(List<string> targetLanguageCodes, CancellationToken cancellationToken)
    {
        var targetLanguages = targetLanguageCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), DefaultConfigFileName);

        if (!File.Exists(configPath))
        {
            var defaultConfigJson = ReadEmbeddedResourceText(options.DefaultConsoleModelDrivenAiTraslatorConfigResourceSuffix);
            var defaultConfig = ConsoleModelDrivenAiTraslatorConfigJsonService.ParseConsoleModelDrivenAiTraslatorConfig(defaultConfigJson);
            var filteredConfig = ConsoleModelDrivenAiTraslatorConfigJsonService.BuildConfigForTargetLanguages(defaultConfig, targetLanguages);
            var outputJson = ConsoleModelDrivenAiTraslatorConfigJsonService.SerializeConsoleModelDrivenAiTraslatorConfig(filteredConfig);
            await File.WriteAllTextAsync(configPath, outputJson, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        }

        var json = await File.ReadAllTextAsync(configPath, cancellationToken).ConfigureAwait(false);
        translatorConfig = ConsoleModelDrivenAiTraslatorConfigJsonService.ParseConsoleModelDrivenAiTraslatorConfig(json);
    }

    private string ReadEmbeddedResourceText(string resourceSuffix)
    {
        var assembly = typeof(GenerateExecutor).Assembly;
        var resourceName = assembly
            .GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith(resourceSuffix, StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
        {
            throw new InvalidOperationException($"Embedded resource '{resourceSuffix}' not found.");
        }

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            throw new InvalidOperationException($"Unable to open embedded resource '{resourceName}'.");
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static HashSet<string>? ParseFilterSet(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string ComputeChecksum(string text)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

}
