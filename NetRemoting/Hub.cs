using Castle.DynamicProxy;
using NetRemoting.Communication;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace NetRemoting;

public class Hub
{
    private readonly Hub _parentHub;
    private readonly List<Hub> _childrenHubs = new List<Hub>();

    private readonly Func<byte[], bool> _dataSender;
    private readonly HubRegistry _registry = new HubRegistry();

    private readonly ConcurrentDictionary<Instance, IRemoteObjectImplementation> _handlers = new ConcurrentDictionary<Instance, IRemoteObjectImplementation>();
    private readonly ConcurrentDictionary<Instance, ICaller> _callers = new ConcurrentDictionary<Instance, ICaller>();
    private readonly ConcurrentDictionary<Instance, object> _remotes = new ConcurrentDictionary<Instance, object>();
    private readonly ConcurrentDictionary<IRemoteObjectImplementation, ICaller> _handlerCallers = new ConcurrentDictionary<IRemoteObjectImplementation, ICaller>();

    public Guid ClientId { get; }

    public bool IsClientHub => _parentHub == null && ClientId != Guid.Empty;

    public HubRegistry Registry => _registry;

    public Hub(Hub parentHub, Guid clientId, Func<byte[], bool> sender)
    {
        _parentHub = parentHub;
        parentHub?._childrenHubs.Add(this);

        ClientId = clientId;
        _dataSender = sender;
    }

    public IRemoteObjectImplementation InstantiateSingleton<T>(object impl)
    {
        var type = typeof(T);
        var o = CreateRemoteObjectImplementationFor(type, impl);
        var c = CreateCallerFor(new Instance(type.Name));
        RegisterImpementation(c, o);
        return o;
    }

    private void RegisterImpementation(ICaller caller, IRemoteObjectImplementation handler)
    {
        caller.CallReceived += Caller_CallReceived;
        handler.Event += Handler_Event;

        _ = _handlers.TryAdd(caller.Instance, handler);  // TODO check

        _ = _handlerCallers.TryAdd(handler, caller);  // TODO check
    }

    private void Handler_Event(object sender, CallInfo callInfo)
    {
        var handler = (IRemoteObjectImplementation)sender;
        if (!_handlerCallers.TryGetValue(handler, out var caller))
        {
            throw new Exception($"Caller not found for handler: `{handler.GetType()}`.");
        }

        var eventCall = new EventCall(new Signature
        {
            ServiceName = caller.Instance.ServiceName,
            InstanceId = caller.Instance.InstanceId,
            MethodName = callInfo.Name,
        },
            callInfo.Arguments);

        caller.RaiseEvent(callInfo.ThreadId, eventCall);
    }

    private void Caller_CallReceived(object sender, Request request)
    {
        var caller = (ICaller)sender;
        if (!_handlers.TryGetValue(caller.Instance, out var handler))
        {
            throw new Exception($"Handler not found for caller: `{caller.Instance}`.");
        }

        var processor = GetSenders().Single(x => x.ClientId == request.ClientId);
        var resultMessage = processor.ProcessMethodCall(handler, request);
        caller.Send(request.ResponseWith(resultMessage));
    }

    internal bool TryGetRemote(Instance instance, out object remote)
    {
        return _remotes.TryGetValue(instance, out remote);
    }

    internal bool TryGetRemoteOrImplementation(Instance instance, out object o)
    {
        if (TryGetRemote(instance, out o))
        {
            return true;
        }

        if (_handlers.TryGetValue(instance, out var impl))
        {
            o = impl.Target;
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
        var methodCall = (MethodCall)request.Message.Payload;
        var arguments = methodCall.Arguments?.ToArray();
        var callInfo = new CallInfo(request.Message.Header.ThreadId, methodCall.Signature.MethodName, arguments)
        {
            Hub = this,
        };
        return callInfo;
    }

    private Message ProcessMethodCall(IRemoteObjectImplementation handler, Request request)
    {
        var callInfo = CreateMethodCallInfo(request);
        var originalArguments = callInfo.Arguments.Select(x => x.Value).ToArray();

        try
        {
            var resultObject = handler.Call(callInfo);
            var o = resultObject is Object obj
                ? obj
                : Object.Create(resultObject?.GetType(), resultObject);
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
        if (resultObject is null)
        {
            throw new ArgumentNullException(nameof(resultObject));
        }

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
            else if (interfaces.Length == 1)
            {
                var @interface = interfaces.First();
                var server = CreateRemoteObjectImplementationFor(@interface, resultObject.Value);
                var clientHub = GetSenders().First(x => x.ClientId == request.ClientId);
                var id = clientHub._callers.Count(x => x.Key.ServiceName == type.Name) == 0 ? Guid.Empty : Guid.NewGuid();
                var caller = clientHub.CreateCallerFor(new Instance(type.Name, id));

                RegisterImpementation(caller, server);

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

        if (_parentHub._registry.TryGetClientFactory(type, out var parentFactory))
        {
            return parentFactory(caller);
        }

        return RemoteObject.For(type, caller);

        throw new InvalidOperationException($"Client factory for `{type}` and `{caller}` not registered.");
    }

    public IRemoteObjectImplementation CreateRemoteObjectImplementationFor(Type type, object impl)
    {
        if (_registry.TryGetServerFactory(type, out var factory))
        {
            return factory(impl);
        }

        if (_parentHub != null &&
            _parentHub._registry.TryGetServerFactory(type, out var parentFactory))
        {
            return parentFactory(impl);
        }

        return new RemoteObjectImplementation(type, impl);

        throw new InvalidOperationException($"Server factory for `{type}` not registered.");
    }

    public ICaller CreateCallerWithInstance(Type type, object impl)
    {
        var caller = CreateCallerFor(new Instance(type.Name, Guid.NewGuid()));
        var implementation = CreateRemoteObjectImplementationFor(type, impl);
        RegisterImpementation(caller, implementation);

        return caller;
    }

    public ICaller CreateCallerWithInstance(Type type, Delegate impl)
    {
        var caller = CreateCallerFor(new Instance(type.Name, Guid.NewGuid()));
        var implementation = new RemoteObjectImplementation(type, impl);
        RegisterImpementation(caller, implementation);

        return caller;
    }

    public ICaller CreateCallerFor(Instance instance)
    {
        return _callers.GetOrAdd(instance, x => new Caller(this, x));
    }

    public void RemoveCaller(Instance instance)
    {
        // TODO
        //if (!_callers.TryRemove(instance, out _))
        //{
        //    throw new Exception($"Could not remove caller instance: `{instance}`");
        //}

        //WriteLine($"Removed caller instance: {instance}");
    }

    public void SendMessage(Message message)
    {
        var messageString = SerializationHelper.FormatMessage(message);
        SendString(messageString);
    }

    private void SendString(string data)
    {
        var dataBytes = Encoding.UTF8.GetBytes(data);
        var result = _dataSender(dataBytes);
        if (!result)
        {
            throw new Exception($"Data not send: `{data}`");
        }
    }

    private void WriteLine(string line)
    {
        Debug.WriteLine($"{nameof(Hub)} >>> {line}");
    }

    public void ReceiveRequest(Request request)
    {
        //lock (_locker)
        //{
        //    _queue.Enqueue(request);
        //    _autoResetEvent.Set();
        //}
        var caller = GetCallerFor(request);
        WriteLine($"{caller.Instance} received a request: {request.ClientId} {request.Message}");
        caller.ReceiveRequest(request);
    }

    private ICaller GetCallerFor(Request request)
    {
        var message = request.Message;
        // TODO get instance from message, not its payload.
        switch (message.Header.MessageType)
        {
            case MessageType.MethodCall when message.Payload is MethodCall methodCall:
                return GetCaller(request.ClientId, new Instance(methodCall.Signature.ServiceName, methodCall.Signature.InstanceId));

            case MessageType.MethodCallResult when message.Payload is MethodCallResult callResult:
                return GetCaller(request.ClientId, new Instance(callResult.Signature.ServiceName, callResult.Signature.InstanceId));

            case MessageType.Event when message.Payload is EventCall eventCall:
                return GetCaller(request.ClientId, new Instance(eventCall.Signature.ServiceName, eventCall.Signature.InstanceId));

            case MessageType.EventResponse when message.Payload is EventResponse eventResponse:
                return GetCaller(request.ClientId, new Instance(eventResponse.Signature.ServiceName, eventResponse.Signature.InstanceId));

            default:
                throw new NotSupportedException($"{message.Header.MessageType} with {message.Payload.GetType()}");
        }
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

        throw new Exception($"Caller for instance was not found: `{instance}`");
    }

    public IEnumerable<Hub> GetSenders()
    {
        return new[] { this }.Concat(_childrenHubs);
    }

    public static Hub CreateMainHub() => new Hub(null, Guid.Empty, x => true);
}
