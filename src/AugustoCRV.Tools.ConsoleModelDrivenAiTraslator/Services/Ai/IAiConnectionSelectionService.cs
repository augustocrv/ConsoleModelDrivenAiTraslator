namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services;

internal interface IAiConnectionSelectionService
{
    Task<AiConnection?> GetSelectedConnectionAsync(CancellationToken cancellationToken);

    Task SetSelectedConnectionAsync(string connectionName, CancellationToken cancellationToken);
}
