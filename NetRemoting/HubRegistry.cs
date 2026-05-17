namespace NetRemoting;

public class HubRegistry
{
    private readonly Dictionary<Type, Func<ICaller, object>> _clientFactories = new Dictionary<Type, Func<ICaller, object>>();
    private readonly Dictionary<Type, Func<object, IRemoteObjectImplementation>> _serverFactories = new Dictionary<Type, Func<object, IRemoteObjectImplementation>>();

    public void RegisterFactory<T>(Func<ICaller, T> clientFactory)
    {
        _clientFactories.Add(typeof(T), (instance) => clientFactory(instance));
    }

    public void RegisterFactory<T>(Func<T, IRemoteObjectImplementation> serverFactory)
    {
        _serverFactories.Add(typeof(T), (impl) => serverFactory((T)impl));
    }

    public bool TryGetClientFactory(Type type, out Func<ICaller, object> clientFactory)
    {
        if (_clientFactories.TryGetValue(type, out var factory))
        {
            clientFactory = factory;
            return true;
        }

        clientFactory = null;
        return false;
    }

    public bool TryGetServerFactory(Type type, out Func<object, IRemoteObjectImplementation> serverFactory)
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
