using System.Diagnostics.CodeAnalysis;

namespace NetRemoting;

public class HubRegistry
{
    private readonly Dictionary<Type, Func<ICaller, object>> _clientFactories = [];
    private readonly Dictionary<Type, Func<object, IRemoteObjectImplementation>> _serverFactories = [];

    public void RegisterFactory<T>(Func<ICaller, T> clientFactory)
        where T : class
    {
        _clientFactories.Add(typeof(T), (instance) => clientFactory(instance));
    }

    public void RegisterFactory<T>(Func<T, IRemoteObjectImplementation> serverFactory)
        where T : class
    {
        _serverFactories.Add(typeof(T), (implementation) => serverFactory((T)implementation));
    }

    public bool TryGetClientFactory(Type type, [NotNullWhen(true)] out Func<ICaller, object>? clientFactory)
    {
        if (_clientFactories.TryGetValue(type, out var factory))
        {
            clientFactory = factory;
            return true;
        }

        clientFactory = null;
        return false;
    }

    public bool TryGetServerFactory(Type type, [NotNullWhen(true)] out Func<object, IRemoteObjectImplementation>? serverFactory)
    {
        if (_serverFactories.TryGetValue(type, out var factory))
        {
            serverFactory = factory;
            return true;
        }

        serverFactory = null;
        return false;
    }
}
