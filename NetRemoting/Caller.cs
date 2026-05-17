using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NetRemoting.Communication;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace NetRemoting;

internal class Caller : ICaller
{
    private readonly Hub _hub;

    private readonly ConcurrentDictionary<int, Stack<ThreadWaiter>> _threadWaiters = new ConcurrentDictionary<int, Stack<ThreadWaiter>>();

    private readonly List<Message> _sentMessages = new List<Message>();
    private readonly List<Message> _receivedMessages = new List<Message>();

    public Instance Instance { get; }

    public event EventHandler<CallInfo> Event;
    public event EventHandler<Request> CallReceived;

    public Caller(Hub hub, Instance instance)
    {
        _hub = hub;
        Instance = instance;
    }

    public void ReceiveRequest(Request rawRequest)
    {
        var request = ProcessRawRequest(rawRequest);

        _receivedMessages.Add(request.Message);

        var threadId = request.Message.Header.ThreadId;
        if (_threadWaiters.TryGetValue(threadId, out var waiterStack))
        {
            waiterStack.Peek().ReceiveRequest(request);
            return;
        }

        ProcessRequest(request);
    }

    private Request ProcessRawRequest(Request rawRequest)
    {
        var message = rawRequest.Message;
        switch (message.Header.MessageType)
        {
            case MessageType.MethodCall when message.Payload is MethodCall methodCall:
                var methodArguments = methodCall.Arguments.Select(x => ProcessRawArgument(rawRequest.ClientId, x)).ToArray();
                return new Request(rawRequest.ClientId, new Message(message.Header, new MethodCall(methodCall.Signature, methodArguments)));

            case MessageType.Event when message.Payload is EventCall eventCall:
                var eventArguments = eventCall.Arguments.Select(x => ProcessRawArgument(rawRequest.ClientId, x)).ToArray();
                return new Request(rawRequest.ClientId, new Message(message.Header, new EventCall(eventCall.Signature, eventArguments)));

            case MessageType.MethodCallResult when message.Payload is MethodCallResult callResult:
                return rawRequest;

            case MessageType.EventResponse when message.Payload is EventResponse eventResponse:
                return rawRequest;

            default:
                throw new NotSupportedException($"{message.Header.MessageType} with {message.Payload.GetType()}");
        }
    }

    private Object ProcessRawArgument(Guid clientId, Object argument)
    {
        // Special values.
        if (argument.Type == null &&
            argument.Value is string strVal)
        {
            if (strVal == "<sender>")
            {
                if (!_hub.TryGetRemote(Instance, out var remote))
                {
                    throw new InvalidOperationException($"Sender remote was not found.");
                }

                return Object.Create(typeof(object), remote);  // todo use real passed sender type?
            }

            return argument;
        }

        // Remote delegate refrence.
        if (typeof(Delegate).IsAssignableFrom(argument.Type) &&
            argument.Value is string strVal4 &&
            SerializationHelper.TryParseInstance(strVal4, out var instance4))
        {
            var hub = _hub.GetSenders().First(x => x.ClientId == clientId);
            var caller = hub.CreateCallerFor(instance4);
            var remote = RemoteDelegate.For(argument.Type, caller);
            hub.AddRemote(instance4, remote);
            return Object.Create(argument.Type, remote);
        }


        // Remote object reference.
        if (argument.Type.IsInterface &&
            argument.Value is string strVal2 &&
            SerializationHelper.TryParseInstance(strVal2, out var instance))
        {
            var hub = _hub.GetSenders().First(x => x.ClientId == clientId);
            var caller = hub.CreateCallerFor(instance);
            var remote = RemoteObject.For(argument.Type, caller);
            hub.AddRemote(instance, remote);
            return Object.Create(argument.Type, remote);
        }

        if (argument.Type == typeof(Guid) &&
            argument.Value is string strVal3)
        {
            return Object.Create(argument.Type, Guid.Parse(strVal3), argument.Name);
        }

        if (argument.Value is JObject jObject)
        {
            return Object.Create(argument.Type, jObject.ToObject(argument.Type), argument.Name);
        }

        return argument;
    }

    private void ProcessRequest(Request request)
    {
        switch (request.Message.Header.MessageType)
        {
            case MessageType.Event:
                var eventCallInfo = _hub.CreateMethodCallInfo(request);
                Event?.Invoke(this, eventCallInfo);
                Send(request.ResponseWith(request.Message.CreateEventResponse()));
                break;
            case MessageType.MethodCall:
                CallReceived?.Invoke(this, request);
                break;
            case MessageType.MethodCallResult:
            case MessageType.EventResponse:
                throw new InvalidOperationException($"Waiter should process such request: `{request.Message}`.");
            default:
                throw new NotSupportedException($"{request.Message.Header.MessageType}");
        }
    }

    public void Send(Response response)
    {
        var message = response.Message;
        if (message.Header.MessageType == MessageType.Event ||
            message.Header.MessageType == MessageType.MethodCall)
        {
            throw new InvalidOperationException($"{message.Header.MessageType} not allowed here.");
        }

        _sentMessages.Add(message);

        var sender = _hub.GetSenders().First(x => x.ClientId == response.Request.ClientId);
        sender.SendMessage(message);
        WriteLine($"SENT message to `{response.Request.ClientId}`: `{message}`");
    }

    public void RaiseEvent(int threadId, EventCall eventCall)
    {
        var childSenders = _hub.GetSenders().Where(x => x.ClientId != Guid.Empty).ToArray();
        var header = new MessageHeader
        {
            ThreadId = threadId,
            Id = Guid.NewGuid(),
            MessageType = MessageType.Event,
        };
        var message = new Message(header, eventCall);

        foreach (var sender in childSenders)
        {
            var responseMessage = WaitForResponse(m => sender.SendMessage(m), message);
            if (!responseMessage.Header.MessageType.Equals(MessageType.EventResponse))
            {
                throw new Exception($"Invalid event response: {responseMessage}.");
            }
        }
    }

    private MethodCall CreateMethodCall(string methodName, Object[] args = null)
    {
        var arguments = args?
            .Select(ProcessArgument)
            .ToArray();

        var call = new MethodCall(new Signature
        {
            ServiceName = Instance.ServiceName,
            InstanceId = Instance.InstanceId,
            MethodName = methodName,
        },
            arguments);
        return call;
    }

    private object ProcessArgument(object argument)
    {
        if (argument == null)
            return null;

        var type = argument.GetType();

        if (type == typeof(Instance))
        {
            return argument.ToString();
        }

        if (type.IsClass && !type.IsArray && type != typeof(string))
        {
            var interfaces = type.GetInterfaces();
            if (interfaces.Length > 1)
            {
                throw new ArgumentException("What interface to take?");
            }
            else if (interfaces.Length == 1)
            {
                var @interface = interfaces.First();
                var caller = _hub.CreateCallerWithInstance(@interface, argument);
                return JsonConvert.SerializeObject(Object.Create(@interface, caller.Instance.ToString()));
            }
        }

        return JsonConvert.SerializeObject(Object.Create(type, argument));
    }

    private Object ProcessArgument(Object argument)
    {
        if (argument == null)
            throw new ArgumentNullException(nameof(argument));

        if (argument.Value == null)
            return argument;

        var type = argument.Type;

        if (type == typeof(Instance))
        {
            return Object.Create(typeof(string), argument.Value.ToString(), argument.Name);
        }

        if (typeof(Delegate).IsAssignableFrom(type))
        {
            var caller = _hub.CreateCallerWithInstance(type, (Delegate)argument.Value);
            var newArgument = Object.Create(type, caller.Instance.ToString(), argument.Name);
            return newArgument;
        }

        if (type.IsInterface)
        {
            var caller = _hub.CreateCallerWithInstance(type, argument.Value);
            var newArgument = Object.Create(type, caller.Instance.ToString(), argument.Name);
            return newArgument;
        }

        if (type.IsClass && !type.IsArray && type != typeof(string))
        {
            var interfaces = type.GetInterfaces();
            if (interfaces.Length > 1)
            {
                throw new ArgumentException("What interface to take?");
            }
            else if (interfaces.Length == 1)
            {
                var @interface = interfaces.First();
                var caller = _hub.CreateCallerWithInstance(@interface, argument.Value);
                var newArgument = Object.Create(@interface, caller.Instance.ToString(), argument.Name);
                return newArgument;
            }
        }

        return argument;
    }

    internal Object Call(Type type, Object[] args, [CallerMemberName] string methodName = null)
    {
        var call = CreateMethodCall(methodName, args);
        var callResult = Call(type, call, out var outArgs);
        foreach (var outArg in outArgs)
        {
            args.First(x => x.Name == outArg.Name).Value = outArg.Value;
        }
        return callResult;
    }

    private Object Call(Type returnType, MethodCall call, out Object[] outArguments)
    {
        WriteLine($"Method call: {call}");
        var response = MakeRemoteCall(call, out outArguments);
        WriteLine($"Call result: {response}");
        if (response.Type != returnType)
        {
            throw new InvalidCastException("Return type mismatch.");
        }

        return response;
    }

    public ICallConfiguration Method([CallerMemberName] string methodName = null)
    {
        return new CallConfiguration(this, methodName);
    }

    public void SetProxy(object proxy)
    {
        _hub.AddRemote(Instance, proxy);
    }

    private Object MakeRemoteCall(MethodCall call, out Object[] outArgs)
    {
        var requestMessage = new Message(MessageType.MethodCall, call);
        var responseMessage = WaitForResponse(m => _hub.SendMessage(m), requestMessage);
        var response = (MethodCallResult)responseMessage.Payload; // TODO check that type matches
        outArgs = response.OutArguments;

        if (response.ResultValue.IsVoid)
        {
            return Return.Void;
        }

        if (response.ResultValue.Exception != null)
        {
            throw response.ResultValue.Exception;
        }

        return ProcessRawResult(response.ResultValue.Result);
    }

    private Object ProcessRawResult(Object obj)
    {
        if (obj == null)
        {
            throw new ArgumentNullException(nameof(obj));
        }

        var type = obj.Type;
        var value = obj.Value;

        // TODO: some general solution for this
        if (obj.Type == typeof(uint) &&
            obj.Value is long longVal)
        {
            return Object.Create(obj.Type, Convert.ToUInt32(longVal));
        }

        if (type == typeof(Guid))
        {
            return Object.Create(type, Guid.Parse((string)value));
        }

        // TODO: date time, offset, etc.

        if (type.IsInterface &&
            type != typeof(IRemoteObjectImplementation) &&
            SerializationHelper.TryParseInstance((string)value, out var instance))
        {
            var caller = _hub.CreateCallerFor(instance);
            // Feature: actor can use direct reference to remote or implementation registered in its hub
            // without creating duplicate remote each time same instance in received back from other actor.
            var remote = _hub.TryGetRemoteOrImplementation(instance, out var val)
                ? val
                : RemoteObject.For(type, caller);
            _hub.AddRemote(instance, remote);
            return Object.Create(type, remote);
        }

        return obj;
    }

    private Message WaitForResponse(Action<Message> sender, Message message)
    {
        _sentMessages.Add(message);

        var waiter = new ThreadWaiter(message);
        waiter.Request += Waiter_Request;

        var waiterStack = _threadWaiters.GetOrAdd(waiter.ThreadId, _ => new Stack<ThreadWaiter>());
        waiterStack.Push(waiter);

        Task.Run(() => sender(message)); // Not to receive response before waiting.
        var waitResult = waiter.Wait();
        waiter.Request -= Waiter_Request;

        var popped = waiterStack.Pop();
        if (waiter != popped)
        {
            throw new Exception("Invalid state");
        }

        if (waiterStack.Count == 0)
        {
            _ = _threadWaiters.TryRemove(waiter.ThreadId, out _); // TODO check
        }

        return waitResult;
    }

    private void Waiter_Request(object sender, Request request)
    {
        ProcessRequest(request);
    }

    private void WriteLine(string line)
    {
        Debug.WriteLine($"{nameof(Caller)} >>> {line}");
    }
}
