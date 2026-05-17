using Castle.DynamicProxy;
using System;

namespace NetRemoting;

public static class RemoteObject
{
    private readonly static ProxyGenerator s_proxyGenerator = new ProxyGenerator();

    public static T For<T>(ICaller caller)
    {
        var proxy = (T)For(typeof(T), caller);
        return proxy;
    }

    public static object For(Type type, ICaller caller)
    {
        var proxy = s_proxyGenerator.CreateInterfaceProxyWithoutTarget(type, new RemoteObjectProxyInterceptor(caller));
        caller.SetProxy(proxy);
        return proxy;
    }
}
