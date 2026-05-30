namespace NetRemoting.Communication;

public class EventCall : MethodCall
{
    public EventCall(Signature signature, Object[] arguments)
        : base(signature, arguments)
    {
    }

    public static EventCall Create(Instance instance, string eventName, Object[] args)
    {
        Signature signature = new()
        {
            ServiceName = instance.ServiceName,
            InstanceId = instance.InstanceId,
            MethodName = eventName,
        };
        return new EventCall(signature, args);
    }
}
