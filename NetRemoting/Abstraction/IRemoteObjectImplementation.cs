using NetRemoting.Communication;
using System;

namespace NetRemoting;

public interface IRemoteObjectImplementation
{
    object Target { get; }

    event EventHandler<CallInfo> Event;

    object Call(CallInfo callInfo);
}
