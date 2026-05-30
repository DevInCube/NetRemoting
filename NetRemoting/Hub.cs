using Castle.DynamicProxy;
using NetRemoting.Communication;
using NetRemoting.Communication.Serializers;
using NetRemoting.Exceptions;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace NetRemoting;

public class Hub
{
    private readonly Hub? _parentHub;
    private readonly List<Hub> _childrenHubs = [];

    private readonly Func<byte[], bool> _dataSender;
    private readonly HubRegistry _registry = new();
    private readonly IMessageSerializer _serializer;

    private readonly ConcurrentDictionary<Instance, IRemoteObjectImplementation> _handlers = new();
    private readonly ConcurrentDictionary<Instance, ICaller> _callers = new();
    private readonly ConcurrentDictionary<Instance, object> _remotes = new();
    private readonly ConcurrentDictionary<IRemoteObjectImplementation, ICaller> _handlerCallers = new();

    public Guid ClientId { get; }

    public bool IsClientHub => _parentHub == null && ClientId != Guid.Empty;

    public HubRegistry Registry => _registry;

    public Hub(Hub? parentHub, Guid clientId, Func<byte[], bool> sender, IMessageSerializer? serializer = null)
    {
        _parentHub = parentHub;
        parentHub?._childrenHubs.Add(this);

        ClientId = clientId;
        _dataSender = sender;
        _serializer = serializer ?? parentHub?._serializer ?? CustomMessageSerializer.Instance;
    }

    public IRemoteObjectImplementation InstantiateSingleton<T>(object impl)
    {
        var type = typeof(T);
        var o = CreateRemoteObjectImplementationFor(type, impl);
        var c = CreateCallerFor(new Instance(type.Name));
        RegisterImplementation(c, o);
        return o;
    }

    private void RegisterImplementation(ICaller caller, IRemoteObjectImplementation handler)
    {
        caller.CallReceived += Caller_CallReceived;
        handler.Event += Handler_Event;

        _ = _handlers.TryAdd(caller.Instance, handler);  // TODO check

        _ = _handlerCallers.TryAdd(handler, caller);  // TODO check
    }

    private void Handler_Event(object? sender, CallInfo callInfo)
    {
        if (sender is not IRemoteObjectImplementation handler)
        {
            return;
        }

        if (!_handlerCallers.TryGetValue(handler, out var caller))
        {
            throw new NetRemotingException($"Caller not found for handler: `{handler.GetType()}`.");
        }

        var signature = new Signature
        {
            ServiceName = caller.Instance.ServiceName,
            InstanceId = caller.Instance.InstanceId,
            MethodName = callInfo.Name,
        };
        var eventCall = new EventCall(signature, callInfo.Arguments);

        caller.RaiseEvent(callInfo.ThreadId, eventCall);
    }

    private void Caller_CallReceived(object? sender, Request request)
    {
        if (sender is not ICaller caller)
        {
            return;
        }

        if (!_handlers.TryGetValue(caller.Instance, out var handler))
        {
            throw new NetRemotingException($"Handler not found for caller: `{caller.Instance}`.");
        }

        var processor = GetSenders().Single(x => x.ClientId == request.ClientId);
        var resultMessage = processor.ProcessMethodCall(handler, request);
        caller.Send(request.ResponseWith(resultMessage));
    }

    internal bool TryGetRemote(Instance instance, [NotNullWhen(true)] out object? remote)
    {
        return _remotes.TryGetValue(instance, out remote);
    }

    internal bool TryGetRemoteOrImplementation(Instance instance, [NotNullWhen(true)] out object? o)
    {
        if (TryGetRemote(instance, out o))
        {
            return true;
        }

        if (_handlers.TryGetValue(instance, out var handler))
        {
            o = handler.Target;
            return true;
        }

        o = null;
        return false;
    }

    internal void AddRemote(Instance instance, object remote)
    {
        _ = _remotes.TryAdd(instance, remote);
    }

    internal CallInfo CreateMethodCallInfo(Request request)
    {
        if (request.Message.Payload is not MethodCall methodCall)
        {
            throw new ArgumentException("Request message payload is not a method call.");
        }

        var arguments = methodCall.Arguments?.ToArray() ?? [];
        var callInfo = new CallInfo(request.Message.Header.ThreadId, methodCall.Signature.MethodName, arguments)
        {
            Hub = this,
        };
        return callInfo;
    }

    private Message ProcessMethodCall(IRemoteObjectImplementation handler, Request request)
    {
        var callInfo = CreateMethodCallInfo(request);

        // TODO should this be used?
        var originalArguments = callInfo.Arguments.Select(x => x.Value).ToArray();

        try
        {
            var resultObject = handler.Call(callInfo);
            var resultType = resultObject?.GetType();
            var o = resultObject is Object obj
                ? obj
                : Object.Create(resultType, resultObject);
            var outArgs = callInfo.Arguments.Where(x => x.Type.IsByRef).ToArray();
            var resultMessage = ProcessMethodCallResult(request, o, outArgs);
            return resultMessage;
        }
        catch (Exception ex)
        {
            return request.Message.CreateThrowException(ex);
        }
    }

    private Message ProcessMethodCallResult(Request request, Object resultObject, Object[] outArguments)
    {
        ArgumentNullException.ThrowIfNull(resultObject);

        if (resultObject == Return.Void)
        {
            return request.Message.CreateReturnVoid(outArguments);
        }

        var type = resultObject.Type;
        if (type.IsClass && !type.IsArray && type != typeof(string))
        {
            var actualInterfaces = type.GetInterfaces();
            var interfaces = actualInterfaces.Where(x => x != typeof(IDisposable)).ToArray();

            if (interfaces.Any(x => x == typeof(IProxyTargetAccessor)))
            {
                var @interface = interfaces.First();
                var remote = resultObject.Value;
                var clientHub = GetSenders().First(x => x.ClientId == request.ClientId);
                var callerInstance = clientHub._remotes.First(x => x.Value == remote).Key;
                var result = SerializationHelper.FormatInstance(callerInstance);
                var newResultObject = Object.Create(@interface, result);
                return request.Message.CreateReturn(newResultObject, outArguments);
            }

            if (interfaces.Length > 1)
            {
                throw new ArgumentException("What interface to take?");
            }

            if (interfaces.Length == 1)
            {
                var @interface = interfaces.First();
                var server = CreateRemoteObjectImplementationFor(@interface, resultObject.Value);
                var clientHub = GetSenders().First(x => x.ClientId == request.ClientId);
                var id = !clientHub._callers.Any(x => x.Key.ServiceName == type.Name) ? Guid.Empty : Guid.NewGuid();
                var caller = clientHub.CreateCallerFor(new Instance(type.Name, id));

                RegisterImplementation(caller, server);

                var result = SerializationHelper.FormatInstance(caller.Instance);
                var newResultObject = Object.Create(@interface, result);
                return request.Message.CreateReturn(newResultObject, outArguments);
            }
        }

        return request.Message.CreateReturn(resultObject, outArguments);
    }

    public object CreateHandlerFor(Type type, ICaller caller)
    {
        if (_registry.TryGetClientFactory(type, out var factory))
        {
            return factory(caller);
        }

        if (_parentHub != null &&
            _parentHub._registry.TryGetClientFactory(type, out var parentFactory))
        {
            return parentFactory(caller);
        }

        return RemoteObject.For(type, caller);

        throw new InvalidOperationException($"Client factory for `{type}` and `{caller}` not registered.");
    }

    public IRemoteObjectImplementation CreateRemoteObjectImplementationFor(Type type, object implementation)
    {
        if (_registry.TryGetServerFactory(type, out var factory))
        {
            return factory(implementation);
        }

        if (_parentHub != null &&
            _parentHub._registry.TryGetServerFactory(type, out var parentFactory))
        {
            return parentFactory(implementation);
        }

        return new RemoteObjectImplementation(type, implementation);

        throw new InvalidOperationException($"Server factory for `{type}` not registered.");
    }

    public ICaller CreateCallerWithInstance(Type type, object implementation)
    {
        var caller = CreateCallerFor(new Instance(type.Name, Guid.NewGuid()));
        var remoteImplementation = CreateRemoteObjectImplementationFor(type, implementation);
        RegisterImplementation(caller, remoteImplementation);

        return caller;
    }

    public ICaller CreateCallerWithInstance(Type type, Delegate implementation)
    {
        var caller = CreateCallerFor(new Instance(type.Name, Guid.NewGuid()));
        var remoteImplementation = new RemoteObjectImplementation(type, implementation);
        RegisterImplementation(caller, remoteImplementation);

        return caller;
    }

    public ICaller CreateCallerFor(Instance instance)
    {
        return _callers.GetOrAdd(instance, x => new Caller(this, x));
    }

    public void RemoveCaller(Instance instance)
    {
        // TODO
        ////if (!_callers.TryRemove(instance, out _))
        ////{
        ////    throw new NetRemotingException($"Could not remove caller instance: `{instance}`");
        ////}

        ////WriteLine($"Removed caller instance: {instance}");
    }

    public void SendMessage(Message message)
    {
        var messageString = _serializer.FormatMessage(message);
        SendString(messageString);
    }

    private void SendString(string data)
    {
        var dataBytes = Encoding.UTF8.GetBytes(data);
        var result = _dataSender(dataBytes);
        if (!result)
        {
            throw new NetRemotingException($"Data not send: `{data}`");
        }
    }

    private void WriteLine(string line)
    {
        Debug.WriteLine($"{nameof(Hub)} >>> {line}");
    }

    public void ReceiveRequest(Request request)
    {
        ////lock (_locker)
        ////{
        ////    _queue.Enqueue(request);
        ////    _autoResetEvent.Set();
        ////}
        var caller = GetCallerFor(request);
        WriteLine($"{caller.Instance} received a request: {request.ClientId} {request.Message}");
        caller.ReceiveRequest(request);
    }

    private ICaller GetCallerFor(Request request)
    {
        var message = request.Message;
        // TODO get instance from message, not its payload.
        return message.Header.MessageType switch
        {
            MessageType.MethodCall when message.Payload is MethodCall methodCall
                => GetCaller(request.ClientId, new Instance(methodCall.Signature.ServiceName, methodCall.Signature.InstanceId)),
            MessageType.MethodCallResult when message.Payload is MethodCallResult callResult
                => GetCaller(request.ClientId, new Instance(callResult.Signature.ServiceName, callResult.Signature.InstanceId)),
            MessageType.Event when message.Payload is EventCall eventCall
                => GetCaller(request.ClientId, new Instance(eventCall.Signature.ServiceName, eventCall.Signature.InstanceId)),
            MessageType.EventResponse when message.Payload is EventResponse eventResponse
                => GetCaller(request.ClientId, new Instance(eventResponse.Signature.ServiceName, eventResponse.Signature.InstanceId)),
            _ => throw new NotSupportedException($"{message.Header.MessageType} with {message.Payload?.GetType()}"),
        };
    }

    private ICaller GetCaller(Guid clientId, Instance instance)
    {
        if ((ClientId == clientId || ClientId == Guid.Empty || IsClientHub) &&
            _callers.TryGetValue(instance, out var handler))
        {
            return handler;
        }

        var childHub = _childrenHubs
            .FirstOrDefault(x => x.ClientId == clientId && x._callers.TryGetValue(instance, out _));
        if (childHub != null &&
            childHub._callers.TryGetValue(instance, out var childCaller))
        {
            return childCaller;
        }

        if ((_parentHub?.ClientId == clientId || _parentHub?.ClientId == Guid.Empty || _parentHub?.IsClientHub == true) &&
            _parentHub._callers.TryGetValue(instance, out var parentCaller))
        {
            return parentCaller;
        }

        throw new NetRemotingException($"Caller for instance was not found: `{instance}`");
    }

    public IEnumerable<Hub> GetSenders()
    {
        return new[] { this }.Concat(_childrenHubs);
    }

    public static Hub CreateMainHub(IMessageSerializer? serializer = null) => new(null, Guid.Empty, x => true, serializer);
}
