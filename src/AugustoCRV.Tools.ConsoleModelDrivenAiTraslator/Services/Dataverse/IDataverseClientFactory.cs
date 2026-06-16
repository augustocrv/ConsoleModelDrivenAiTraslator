namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services.Dataverse;

internal interface IDataverseClientFactory
{
    Task<IOrganizationService> CreateAsync(CancellationToken cancellationToken);
}
