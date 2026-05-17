using NetRemoting.CSharp;
using System;
using System.Linq;

namespace NetRemoting;

public static class RemoteDelegate
{
    public static Delegate For(Type delegateType, ICaller caller)
    {
        var invokeMethod = delegateType.GetMethod(Language.DelegateInvokeMethodName);
        var parameters = invokeMethod.GetParameters();
        var returnType = invokeMethod.ReturnType;

        return DelegateHelper.Create(delegateType, args =>
        {
            var arguments = args
                .Select((x, i) => Object.Create(parameters[i].ParameterType, x, parameters[i].Name))
                .ToArray();
            var @object = caller.Method(Language.DelegateInvokeMethodName).Call(returnType, arguments);
            return @object.Value;
        });
    }
}
