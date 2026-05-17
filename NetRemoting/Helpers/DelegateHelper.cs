using NetRemoting.CSharp;
using System.Linq.Expressions;
using System.Reflection;

namespace NetRemoting;

public static class DelegateHelper
{
    public static Delegate Create(EventInfo eventInfo, Action<object[]> action)
    {
        var handlerType = eventInfo.EventHandlerType;
        return Create(handlerType, action);
    }

    public static Delegate Create(Type handlerType, Action<object[]> action)
    {
        var eventParams = handlerType.GetMethod(Language.DelegateInvokeMethodName).GetParameters();

        var parameterExpressions = eventParams.Select((p, i) => Expression.Parameter(p.ParameterType, $"x{i}")).ToArray();
        var parameterArrayExpression = Expression.NewArrayInit(typeof(object), parameterExpressions);
        var body = Expression.Call(Expression.Constant(action), action.GetType().GetMethod(Language.DelegateInvokeMethodName), parameterArrayExpression);
        var lambda = Expression.Lambda(body, parameterExpressions);
        return Delegate.CreateDelegate(handlerType, lambda.Compile(), Language.DelegateInvokeMethodName, false);
    }

    public static Delegate Create(Type delegateType, Func<object[], object> function)
    {
        var delegateInvokeMethod = delegateType.GetMethod(Language.DelegateInvokeMethodName);
        var delegateParameters = delegateInvokeMethod.GetParameters();
        var delegateReturnType = delegateInvokeMethod.ReturnType;

        var parameterExpressions = delegateParameters
            .Select((p, i) => Expression.Parameter(p.ParameterType, $"x{i}"))
            .ToArray();
        var parameterArrayExpression = Expression.NewArrayInit(typeof(object), parameterExpressions);
        var functionInvokeMethod = function.GetType().GetMethod(Language.DelegateInvokeMethodName);
        var functionCallExpression = Expression.Call(Expression.Constant(function), functionInvokeMethod, parameterArrayExpression);
        var bodyExpression = Expression.Convert(functionCallExpression, delegateReturnType);
        var lambdaExpression = Expression.Lambda(bodyExpression, parameterExpressions);

        var newDelegate = Delegate.CreateDelegate(delegateType, lambdaExpression.Compile(), Language.DelegateInvokeMethodName, ignoreCase: false);
        return newDelegate;
    }
}
