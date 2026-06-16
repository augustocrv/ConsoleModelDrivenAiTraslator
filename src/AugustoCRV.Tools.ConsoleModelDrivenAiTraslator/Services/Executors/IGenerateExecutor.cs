namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services;

/// <summary>
/// Defines orchestration for export and translation generation.
/// </summary>
internal interface IGenerateExecutor
{
    /// <summary>
    /// Executes generation and optional translation for a Dataverse solution.
    /// </summary>
    Task<int> ExecuteAsync(
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
        CancellationToken cancellationToken);
}







