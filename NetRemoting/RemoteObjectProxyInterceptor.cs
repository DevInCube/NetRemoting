using Castle.DynamicProxy;
using NetRemoting.CSharp;
using System;
using System.Linq;

namespace NetRemoting;

internal class RemoteObjectProxyInterceptor : IInterceptor
{
    private readonly ICaller _caller;
    private readonly EventListeners _eventListeners = new EventListeners();

    public RemoteObjectProxyInterceptor(ICaller caller)
    {
        _caller = caller ?? throw new ArgumentNullException(nameof(caller));
        caller.Event += Caller_Event;
    }

    private void Caller_Event(object sender, CallInfo callInfo)
    {
        var eventName = callInfo.Name;

        var args = callInfo.Arguments
            .Select(x => x.Value)
            .ToArray();
        foreach (var handler in _eventListeners.GetInvocationList(eventName))
        {
            handler.Method.Invoke(handler.Target, args);
        }
    }

    public void Intercept(IInvocation invocation)
    {
        if (invocation.Method.Name.StartsWith(Language.AddPrefix))
        {
            var name = invocation.Method.Name.Substring(Language.AddPrefix.Length);
            _eventListeners.Add(name, (Delegate)invocation.Arguments[0]);
            return;
        }

        if (invocation.Method.Name.StartsWith(Language.RemovePrefix))
        {
            var name = invocation.Method.Name.Substring(Language.RemovePrefix.Length);
            _eventListeners.Remove(name, (Delegate)invocation.Arguments[0]);
            return;
        }

        var parameters = invocation.Method.GetParameters();
        var arguments = invocation.Arguments
            .Select((x, i) => Object.Create(parameters[i].ParameterType, x, parameters[i].Name))
            .ToArray();

        var obj = _caller
            .Method(invocation.Method.Name)
            .Call(invocation.Method.ReturnType, arguments);

        // Fill out parameters.
        foreach (var (outArg, index) in arguments.Select((x, i) => (x, i)))
        {
            invocation.SetArgumentValue(index, outArg.Value);
        }

        invocation.ReturnValue = obj.Value;  // TODO check types
    }
}
