using NetRemoting.Communication;
using System.Runtime.CompilerServices;

namespace NetRemoting;

public interface ICaller
{
    Instance Instance { get; }

    event EventHandler<CallInfo> Event;
    event EventHandler<Request> CallReceived;

    void ReceiveRequest(Request request);
    void Send(Response response);
    void RaiseEvent(int threadId, EventCall eventCall);

    ICallConfiguration Method([CallerMemberName] string? methodName = null);
    void SetProxy(object proxy);
}
