using NetRemoting.CSharp;
using NetRemoting.Exceptions;
using System.Linq.Expressions;
using System.Reflection;

namespace NetRemoting;

public static class DelegateHelper
{
    public static Delegate Create(EventInfo eventInfo, Action<object[]> action)
    {
        var handlerType = eventInfo.EventHandlerType
            ?? throw new NetRemotingException($"Handler type is not set.");
        return Create(handlerType, action);
    }

    public static Delegate Create(Type handlerType, Action<object[]> action)
    {
        var delegateMethod = handlerType.GetMethod(Language.DelegateInvokeMethodName)
            ?? throw new NetRemotingException($"Method '{Language.DelegateInvokeMethodName}' was not found on '{handlerType}'.");
        var eventParams = delegateMethod.GetParameters();

        var parameterExpressions = eventParams.Select((p, i) => Expression.Parameter(p.ParameterType, $"x{i}")).ToArray();
        var parameterArrayExpression = Expression.NewArrayInit(typeof(object), parameterExpressions);
        var actionType = action.GetType();
        var actionTypeMethod = actionType.GetMethod(Language.DelegateInvokeMethodName)
            ?? throw new NetRemotingException($"Method '{Language.DelegateInvokeMethodName}' was not found on '{actionType}'.");
        var body = Expression.Call(Expression.Constant(action), actionTypeMethod, parameterArrayExpression);
        var lambda = Expression.Lambda(body, parameterExpressions);
        return Delegate.CreateDelegate(handlerType, lambda.Compile(), Language.DelegateInvokeMethodName, false);
    }

    public static Delegate Create(Type delegateType, Func<object[], object?> function)
    {
        var delegateInvokeMethod = delegateType.GetMethod(Language.DelegateInvokeMethodName)
            ?? throw new NetRemotingException($"Method '{Language.DelegateInvokeMethodName}' was not found on '{delegateType}'.");
        var delegateParameters = delegateInvokeMethod.GetParameters();
        var delegateReturnType = delegateInvokeMethod.ReturnType;

        var parameterExpressions = delegateParameters
            .Select((p, i) => Expression.Parameter(p.ParameterType, $"x{i}"))
            .ToArray();
        var parameterArrayExpression = Expression.NewArrayInit(typeof(object), parameterExpressions);
        var functionType = function.GetType();
        var functionInvokeMethod = functionType.GetMethod(Language.DelegateInvokeMethodName)
            ?? throw new NetRemotingException($"Method '{Language.DelegateInvokeMethodName}' was not found on '{functionType}'.");
        var functionCallExpression = Expression.Call(Expression.Constant(function), functionInvokeMethod, parameterArrayExpression);
        var bodyExpression = Expression.Convert(functionCallExpression, delegateReturnType);
        var lambdaExpression = Expression.Lambda(bodyExpression, parameterExpressions);

        var newDelegate = Delegate.CreateDelegate(delegateType, lambdaExpression.Compile(), Language.DelegateInvokeMethodName, ignoreCase: false);
        return newDelegate;
    }
}
