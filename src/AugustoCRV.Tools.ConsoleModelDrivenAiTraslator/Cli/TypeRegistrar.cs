
namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Cli;

internal sealed class TypeRegistrar : ITypeRegistrar
{
    private readonly IServiceCollection services;

    public TypeRegistrar(IServiceCollection services)
    {
        this.services = services;
    }

    public ITypeResolver Build()
    {
        return new TypeResolver(services.BuildServiceProvider());
    }

    public void Register(Type service, Type implementation)
    {
        services.AddTransient(service, implementation);
    }

    public void RegisterInstance(Type service, object implementation)
    {
        services.AddSingleton(service, implementation);
    }

    public void RegisterLazy(Type service, Func<object> factory)
    {
        if (factory is null)
        {
            throw new ArgumentNullException(nameof(factory));
        }

        services.AddTransient(service, _ => factory());
    }
}

internal sealed class TypeResolver : ITypeResolver, IDisposable
{
    private readonly ServiceProvider provider;

    public TypeResolver(ServiceProvider provider)
    {
        this.provider = provider;
    }

    public object? Resolve(Type? type)
    {
        return type is null ? null : provider.GetService(type);
    }

    public void Dispose()
    {
        provider.Dispose();
    }
}


