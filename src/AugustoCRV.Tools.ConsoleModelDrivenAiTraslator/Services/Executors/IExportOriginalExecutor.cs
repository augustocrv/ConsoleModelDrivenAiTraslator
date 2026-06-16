using AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Models.Translation;

namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services;

/// <summary>
/// Exports only the original workbook from Dataverse metadata.
/// </summary>
internal interface IExportOriginalExecutor
{
    Task<int> ExecuteAsync(
        string solutionName,
        string sourceLcid,
        string? sourceCsvFile,
        string? exportFolder,
        bool enableManaged,
        CancellationToken cancellationToken);

    Task<TranslationWorkbookData> ExportToMemoryAsync(
        string solutionName,
        string sourceLcid,
        bool enableManaged,
        CancellationToken cancellationToken);
}
