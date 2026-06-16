namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services;

internal interface IDataverseConnectionStoreService
{
    Task<List<DataverseConnection>> LoadAsync();
    Task SaveAsync(List<DataverseConnection> connections);
}
