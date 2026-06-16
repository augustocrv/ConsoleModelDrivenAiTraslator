namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services;

/// <summary>
/// Defines orchestration for importing translated content into Dataverse.
/// </summary>
internal interface IPushExecutor
{
    /// <summary>
    /// Executes the translation import operation from a workbook file.
    /// </summary>
    Task<int> ExecuteAsync(
        string workbookPath,
        string? sourceLcid,
        string? targetLcid,
        int? importBatchSize,
        bool force,
        CancellationToken cancellationToken);
}
