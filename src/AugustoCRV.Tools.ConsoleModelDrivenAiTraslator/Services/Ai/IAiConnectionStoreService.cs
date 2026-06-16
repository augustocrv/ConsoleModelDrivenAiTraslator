
namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services
{
    /// <summary>Interface description.</summary>
    public interface IAiConnectionStoreService
    {
        Task<List<AiConnection>> LoadAsync();

        Task SaveAsync(List<AiConnection> connections);
    }
}



