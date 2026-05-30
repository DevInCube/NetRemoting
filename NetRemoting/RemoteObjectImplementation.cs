using NetRemoting.CSharp;
using NetRemoting.Exceptions;
using System.Reflection;

namespace NetRemoting;

public class RemoteObjectImplementation : IRemoteObjectImplementation
{
    private readonly Type _type;
    private readonly object _target;
    private readonly IDictionary<int, int> _threadMapping = new Dictionary<int, int>();

    public object Target => _target;

    public event EventHandler<CallInfo>? Event;

    public RemoteObjectImplementation(Type type, object target)
    {
        _type = type;
        _target = target;

        var events = _type.GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
        foreach (var @event in events)
        {
            var eventHandlerType = @event.EventHandlerType
                ?? throw new NetRemotingException("Event handler was not set.");
            var method = eventHandlerType.GetMethod(Language.DelegateInvokeMethodName)
                ?? throw new NetRemotingException($"Method '{Language.DelegateInvokeMethodName}' was not found on '{eventHandlerType}'.");
            var parameters = method.GetParameters();
            var @delegate = DelegateHelper.Create(@event, args =>
            {
                var arguments = args
                    .Select((x, i) => (x == _target) ? Object.Create(null, "<sender>", parameters[i].Name) : Object.Create(parameters[i].ParameterType, x, parameters[i].Name))
                    .ToArray();
                var managedThreadId = Thread.CurrentThread.ManagedThreadId;
                var threadId = _threadMapping.TryGetValue(managedThreadId, out var val)
                    ? val
                    : managedThreadId;
                Event?.Invoke(this, CallInfo.Create(threadId, @event.Name, arguments));
            });
            @event.AddEventHandler(_target, @delegate);
        }
    }

    public object? Call(CallInfo callInfo)
    {
        _threadMapping[Thread.CurrentThread.ManagedThreadId] = callInfo.ThreadId;

        var type = _type;
        var realArguments = callInfo.Arguments
            .Select(x => x.GetValue())
            .ToArray();

        if (callInfo.MethodType == MethodType.Default)
        {
            var method = type.GetMethod(callInfo.Name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            if (method == null)
            {
                throw new NotSupportedException(callInfo.Name);
            }

            var result = method.Invoke(_target, realArguments);
            for (var i = 0; i < realArguments.Length; i++)
            {
                callInfo.Arguments[i].Value = realArguments[i];
            }

            return method.ReturnType == typeof(void)
                ? Return.Void
                : result;
        }
        else if (callInfo.MethodType == MethodType.PropertyGet ||
                 callInfo.MethodType == MethodType.PropertySet)
        {
            var property = type.GetProperty(callInfo.Name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            if (property == null)
            {
                throw new NotSupportedException(callInfo.Name);
            }

            if (callInfo.MethodType == MethodType.PropertySet)
            {
                property.SetValue(_target, realArguments.First());
                return Return.Void;
            }
            else
            {
                return property.GetValue(_target);
            }
        }

        throw new NotSupportedException($"RemoteObjectImplementation Call {callInfo.MethodType} {callInfo.Name}");
    }

    public static IRemoteObjectImplementation Create<T>(T target)
        where T : class
    {
        return new RemoteObjectImplementation(typeof(T), target);
    }
}
