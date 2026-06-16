namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services;

internal interface IDataverseConnectionSelectionService
{
    Task<DataverseConnection?> GetSelectedConnectionAsync(CancellationToken cancellationToken);

    Task SetSelectedConnectionAsync(string connectionName, CancellationToken cancellationToken);
}
