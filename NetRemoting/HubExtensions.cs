using System;

namespace NetRemoting;

public static class HubExtensions
{
    public static ICaller CreateCallerFor<T>(this Hub hub, Guid? instanceId = null)
    {
        return hub.CreateCallerFor(new Instance(typeof(T).Name, instanceId));
    }
}
