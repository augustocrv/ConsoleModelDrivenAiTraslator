namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services;

internal interface IConnectionExecutors
{
    Task<int> CreateAsync(
        string name,
        AiConnectionType? type,
        string deploymentEndpoint,
        string apiKey,
        string model,
        string description,
        CancellationToken cancellationToken);

    Task<int> DeleteAsync(string name, CancellationToken cancellationToken);

    Task<int> ListAsync(CancellationToken cancellationToken);

    Task<int> SelectAiConnectionAsync(string name, CancellationToken cancellationToken);

    Task<int> CreateDataverseConnectionAsync(string name, string url, CancellationToken cancellationToken);

    Task<int> DeleteDataverseConnectionAsync(string name, CancellationToken cancellationToken);

    Task<int> ListDataverseConnectionsAsync(CancellationToken cancellationToken);

    Task<int> TestDataverseConnectionAsync(CancellationToken cancellationToken);

    Task<int> SelectDataverseConnectionAsync(string name, CancellationToken cancellationToken);
}