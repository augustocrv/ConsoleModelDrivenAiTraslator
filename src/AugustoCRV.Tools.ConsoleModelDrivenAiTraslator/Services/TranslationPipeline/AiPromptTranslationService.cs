using AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Models.TranslationPipeline;
using Microsoft.Extensions.Options;

namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services.TranslationPipeline;

internal sealed class AiPromptTranslationService : ITranslationService
{
    private const int MaxFormatRetryAttempts = 1;

    private readonly IPromptTemplateService promptTemplateService;
    private readonly KeyedServiceFactory keyedServiceFactory;
    private readonly TranslatorCliOptions options;

    public AiPromptTranslationService(
        IPromptTemplateService promptTemplateService,
        KeyedServiceFactory keyedServiceFactory,
        IOptions<TranslatorCliOptions> options)
    {
        this.promptTemplateService = promptTemplateService;
        this.keyedServiceFactory = keyedServiceFactory;
        this.options = options.Value;
    }

    public async Task<TranslationServiceResult> ExecuteAsync(TranslationServiceContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.Connection);

        var rowsToTranslate = context.RowsToTranslate ?? new Dictionary<int, string>();
        if (rowsToTranslate.Count == 0)
        {
            return new TranslationServiceResult();
        }

        var aiService = keyedServiceFactory.GetRequired<IAiService>(context.Connection.Type);

        var additionalPromptRequest = context.AdditionalTranslationContext?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(additionalPromptRequest))
        {
            additionalPromptRequest = "No additional instructions provided.";
        }

        var pagedRequests = BuildPagedRequests(rowsToTranslate, context.Connection, context.TargetLanguageCode, additionalPromptRequest, context.LanguageCodes);
        var translatedRows = new Dictionary<int, string>();

        foreach (var page in pagedRequests)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var response = await aiService
                .TranslateAsync(page.Prompt, context.Connection, cancellationToken)
                .ConfigureAwait(false);

            var translationsBySource = ParseTranslationResponse(response);
            for (var retry = 0; translationsBySource.Count == 0 && retry < MaxFormatRetryAttempts; retry++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var strictRetryPrompt = BuildStrictFormatRetryPrompt(page.Prompt);
                response = await aiService
                    .TranslateAsync(strictRetryPrompt, context.Connection, cancellationToken)
                    .ConfigureAwait(false);

                translationsBySource = ParseTranslationResponse(response);
            }

            if (translationsBySource.Count == 0)
            {
                var diagnosticFilePath = await SaveInvalidResponseDiagnosticAsync(context.Sheet.Name, response, cancellationToken).ConfigureAwait(false);
                throw new InvalidOperationException($"AI response did not contain a valid markdown translation table. Diagnostic saved to '{diagnosticFilePath}'.");
            }

            foreach (var sourceEntry in page.RowsByNormalizedSource)
            {
                if (!translationsBySource.TryGetValue(sourceEntry.Key, out var translatedText) || string.IsNullOrWhiteSpace(translatedText))
                {
                    continue;
                }

                foreach (var row in sourceEntry.Value)
                {
                    translatedRows[row] = translatedText;
                }
            }
        }

        return new TranslationServiceResult
        {
            TranslatedRows = translatedRows
        };
    }

    private List<PagedPromptRequest> BuildPagedRequests(
        Dictionary<int, string> rowsToTranslate,
        AiConnection connection,
        string targetLanguageCode,
        string additionalPromptRequest,
        Dictionary<string, string> languageCodes)
    {
        var rowsByNormalizedSource = BuildRowsByNormalizedSource(rowsToTranslate);
        if (rowsByNormalizedSource.Count == 0)
        {
            return new List<PagedPromptRequest>();
        }

        var targetLanguageName = languageCodes.TryGetValue(targetLanguageCode, out var languageName)
            ? languageName
            : targetLanguageCode;

        var promptTemplate = promptTemplateService.GetTemplate();
        var modelName = ResolveModelName(connection);
        var maxContextTokens = ResolveModelContextWindow(modelName);
        var maxOutputTokens = Math.Clamp(maxContextTokens / 4, 512, 4096);
        var maxInputTokens = Math.Max(1024, maxContextTokens - maxOutputTokens);

        var basePrompt = promptTemplate
            .Replace("{{TARGET_LANGUAGE}}", targetLanguageName)
            .Replace("{{ADDITIONAL_INSTRUCTIONS}}", additionalPromptRequest)
            .Replace("{{BATCH_DATA}}", "| Original | Translation |\n| --- | --- |\n");

        var baseTokens = EstimateTokenCount(basePrompt);
        var availableTokensForRows = Math.Max(256, maxInputTokens - baseTokens);

        var pages = new List<PagedPromptRequest>();
        var currentPage = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        var currentPageSourceTexts = new List<string>();
        var currentTokens = 0;

        foreach (var sourceEntry in rowsByNormalizedSource)
        {
            var sourceText = sourceEntry.Value.SourceText;
            var rowTokens = EstimateTokenCount($"| {EscapeMarkdownCell(sourceText)} |  |\n");

            if (currentPageSourceTexts.Count > 0 && currentTokens + rowTokens > availableTokensForRows)
            {
                pages.Add(CreatePagedPromptRequest(currentPage, currentPageSourceTexts, promptTemplate, targetLanguageName, additionalPromptRequest));
                currentPage = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
                currentPageSourceTexts = new List<string>();
                currentTokens = 0;
            }

            currentPage[sourceEntry.Key] = sourceEntry.Value.RowNumbers;
            currentPageSourceTexts.Add(sourceText);
            currentTokens += rowTokens;
        }

        if (currentPageSourceTexts.Count > 0)
        {
            pages.Add(CreatePagedPromptRequest(currentPage, currentPageSourceTexts, promptTemplate, targetLanguageName, additionalPromptRequest));
        }

        return pages;
    }

    private static Dictionary<string, SourceRows> BuildRowsByNormalizedSource(Dictionary<int, string> rowsToTranslate)
    {
        var grouped = new Dictionary<string, SourceRows>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rowsToTranslate)
        {
            var normalized = NormalizeText(row.Value);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                continue;
            }

            if (!grouped.TryGetValue(normalized, out var sourceRows))
            {
                sourceRows = new SourceRows
                {
                    SourceText = row.Value,
                    RowNumbers = new List<int>()
                };
                grouped[normalized] = sourceRows;
            }

            sourceRows.RowNumbers.Add(row.Key);
        }

        return grouped;
    }

    private static PagedPromptRequest CreatePagedPromptRequest(
        Dictionary<string, List<int>> rowsByNormalizedSource,
        List<string> sourceTexts,
        string promptTemplate,
        string targetLanguageName,
        string additionalPromptRequest)
    {
        var batchMarkdown = BuildInputMarkdownTable(sourceTexts);
        var prompt = promptTemplate
            .Replace("{{TARGET_LANGUAGE}}", targetLanguageName)
            .Replace("{{ADDITIONAL_INSTRUCTIONS}}", additionalPromptRequest)
            .Replace("{{BATCH_DATA}}", batchMarkdown);

        return new PagedPromptRequest
        {
            RowsByNormalizedSource = rowsByNormalizedSource,
            Prompt = prompt
        };
    }

    private static Dictionary<string, string> ParseMarkdownTable(string markdown)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var lines = markdown
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var line in lines)
        {
            if (!line.Contains('|', StringComparison.Ordinal) || line.StartsWith("```", StringComparison.Ordinal))
            {
                continue;
            }

            var cells = SplitMarkdownRow(line);
            if (cells.Count < 2)
            {
                continue;
            }

            if (IsSeparatorRow(cells) || IsHeaderRow(cells))
            {
                continue;
            }

            var source = cells[0].Trim();
            var translation = cells[1].Trim();
            if (string.IsNullOrWhiteSpace(source))
            {
                continue;
            }

            var normalized = NormalizeText(source);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                continue;
            }

            result[normalized] = translation;
        }

        return result;
    }

    private static Dictionary<string, string> ParseTranslationResponse(string response)
    {
        var parsed = ParseMarkdownTable(response);
        if (parsed.Count > 0)
        {
            return parsed;
        }

        foreach (var block in ExtractMarkdownLikeTableBlocks(response))
        {
            parsed = ParseMarkdownTable(block);
            if (parsed.Count > 0)
            {
                return parsed;
            }
        }

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    private static List<string> ExtractMarkdownLikeTableBlocks(string response)
    {
        var lines = (response ?? string.Empty)
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.None);

        var blocks = new List<string>();
        var currentBlock = new List<string>();

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                continue;
            }

            if (line.Contains('|', StringComparison.Ordinal))
            {
                currentBlock.Add(line);
                continue;
            }

            if (currentBlock.Count > 0)
            {
                blocks.Add(string.Join("\n", currentBlock));
                currentBlock.Clear();
            }
        }

        if (currentBlock.Count > 0)
        {
            blocks.Add(string.Join("\n", currentBlock));
        }

        return blocks
            .OrderByDescending(static block => block.Length)
            .ToList();
    }

    private static string BuildStrictFormatRetryPrompt(string originalPrompt)
    {
        return $"""
{originalPrompt}

CRITICAL OUTPUT FORMAT REMINDER:
- Return ONLY a markdown table with exactly two columns: Original | Translation.
- Do NOT add explanations, introductions, notes, or markdown code fences.
- Keep the Original column unchanged and fill Translation for each row.
""";
    }

    private static async Task<string> SaveInvalidResponseDiagnosticAsync(string sheetName, string response, CancellationToken cancellationToken)
    {
        var directory = Path.Combine(Directory.GetCurrentDirectory(), "ai-translator-diagnostics");
        Directory.CreateDirectory(directory);

        var safeSheetName = SanitizeFileNamePart(sheetName);
        var fileName = $"invalid-ai-response_{safeSheetName}_{DateTime.UtcNow:yyyyMMdd_HHmmssfff}_{Guid.NewGuid():N}.txt";
        var fullPath = Path.Combine(directory, fileName);
        var content = string.IsNullOrWhiteSpace(response) ? "<empty AI response>" : response;

        await File.WriteAllTextAsync(fullPath, content, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        return fullPath;
    }

    private static string SanitizeFileNamePart(string value)
    {
        var candidate = string.IsNullOrWhiteSpace(value) ? "UnknownSheet" : value.Trim();
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            candidate = candidate.Replace(invalidChar, '_');
        }

        return string.IsNullOrWhiteSpace(candidate) ? "UnknownSheet" : candidate;
    }

    private static List<string> SplitMarkdownRow(string row)
    {
        var sanitized = row.Trim().Replace("\\|", "{{PIPE}}", StringComparison.Ordinal);
        if (sanitized.StartsWith('|'))
        {
            sanitized = sanitized[1..];
        }
        if (sanitized.EndsWith('|'))
        {
            sanitized = sanitized[..^1];
        }

        return sanitized
            .Split('|')
            .Select(cell => cell.Replace("{{PIPE}}", "|", StringComparison.Ordinal).Trim())
            .ToList();
    }

    private static bool IsSeparatorRow(List<string> cells)
    {
        return cells.All(cell => !string.IsNullOrWhiteSpace(cell) && cell.Trim(':', '-', ' ').Length == 0);
    }

    private static bool IsHeaderRow(List<string> cells)
    {
        if (cells.Count < 2)
        {
            return false;
        }

        return cells[0].Equals("Original", StringComparison.OrdinalIgnoreCase) &&
               cells[1].Equals("Translation", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildInputMarkdownTable(List<string> sourceTexts)
    {
        var builder = new StringBuilder();
        builder.AppendLine("| Original | Translation |");
        builder.AppendLine("| --- | --- |");

        foreach (var sourceText in sourceTexts)
        {
            builder.Append("| ")
                .Append(EscapeMarkdownCell(sourceText))
                .AppendLine(" |  |");
        }

        return builder.ToString();
    }

    private static string EscapeMarkdownCell(string value)
    {
        return (value ?? string.Empty)
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Trim();
    }

    private static string NormalizeText(string text)
    {
        return text?.Trim().ToLowerInvariant() ?? string.Empty;
    }

    private static int EstimateTokenCount(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        return (int)Math.Ceiling(text.Length / 4.0);
    }

    private string ResolveModelName(AiConnection connection)
    {
        if (!string.IsNullOrWhiteSpace(connection.Model))
        {
            return connection.Model;
        }

        return connection.Type == AiConnectionType.GitHubCopilot
            ? options.GitHubCopilotModel
            : "unknown";
    }

    private static int ResolveModelContextWindow(string modelName)
    {
        var normalizedModel = modelName?.Trim().ToLowerInvariant() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedModel))
        {
            return 400000;
        }

        var modelContextWindows = new (string Pattern, int ContextWindow)[]
        {
            ("gpt-5.4", 400000),
            ("gpt-5.3-codex", 400000),
            ("gpt-5.2-codex", 400000),
            ("gpt-5.2", 400000),
            ("gpt-5.1-codex-max", 256000),
            ("gpt-5.1-codex-mini", 256000),
            ("gpt-5.1-codex", 256000),
            ("gpt-5.1", 192000),
            ("gpt-5 mini", 192000),
            ("gpt-5-mini", 192000),
            ("gpt-4.1", 128000),
            ("gpt-4o", 68000),

            ("claude haiku 4.5", 200000),
            ("claude opus 4.5", 200000),
            ("claude opus 4.6", 200000),
            ("claude sonnet 4.6", 200000),
            ("claude sonnet 4.5", 200000),
            ("claude sonnet 4", 144000),

            ("gemini 3.1 pro", 200000),
            ("gemini 3 pro", 200000),
            ("gemini 3 flash", 173000),
            ("gemini 2.5 pro", 173000),

            ("grok code fast 1", 173000),

            ("gpt-35", 16384),
            ("gpt-3.5", 16384)
        };

        foreach (var (pattern, contextWindow) in modelContextWindows)
        {
            if (normalizedModel.Contains(pattern, StringComparison.Ordinal))
            {
                return contextWindow;
            }
        }

        return 400000;
    }

    private sealed class SourceRows
    {
        public required string SourceText { get; init; }

        public required List<int> RowNumbers { get; init; }
    }

    private sealed class PagedPromptRequest
    {
        public required Dictionary<string, List<int>> RowsByNormalizedSource { get; init; }

        public required string Prompt { get; init; }
    }
}
