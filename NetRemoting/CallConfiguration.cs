using System;

namespace NetRemoting;

internal class CallConfiguration : ICallConfiguration
{
    private readonly Caller _caller;
    private readonly string _methodName;

    public CallConfiguration(Caller caller, string methodName)
    {
        _caller = caller;
        _methodName = methodName;
    }

    public Object Call(Type type, params Object[] args)
    {
        return _caller.Call(type, args, _methodName);
    }
}
