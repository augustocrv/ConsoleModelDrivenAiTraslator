namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services;

internal sealed class KeyedServiceFactory
{
    private readonly IServiceProvider serviceProvider;

    public KeyedServiceFactory(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
    }

    public T GetRequired<T>(object key) where T : class
    {
        return serviceProvider.GetRequiredKeyedService<T>(key);
    }
}
