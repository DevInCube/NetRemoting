using System.Collections.Generic;
using System.Linq;

namespace NetRemoting.Communication;

public class EventCall : MethodCall
{
    public EventCall(Signature signature, Object[] arguments)
        : base(signature, arguments)
    {
    }

    public static EventCall Create(Instance instance, string eventName, Object[] args)
    {
        return new EventCall(new Signature
        {
            ServiceName = instance.ServiceName,
            InstanceId = instance.InstanceId,
            MethodName = eventName,
        },
            args);
    }

    public static EventCall Create(string eventName, Object[] args)
    {
        return new EventCall(new Signature
        {
            MethodName = eventName,
        },
            args);
    }
}
