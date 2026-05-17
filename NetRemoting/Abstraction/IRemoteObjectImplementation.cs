namespace NetRemoting;

public interface IRemoteObjectImplementation
{
    object Target { get; }

    event EventHandler<CallInfo> Event;

    object Call(CallInfo callInfo);
}
