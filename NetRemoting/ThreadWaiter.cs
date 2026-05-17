using NetRemoting.Communication;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace NetRemoting;

internal class ThreadWaiter
{
    private readonly ConcurrentQueue<Request> _requests = new ConcurrentQueue<Request>();
    private readonly ConcurrentDictionary<Guid, Message> _responses = new ConcurrentDictionary<Guid, Message>();

    private readonly AutoResetEvent _gotRequestEvent = new AutoResetEvent(false);
    private readonly AutoResetEvent _gotResponseEvent = new AutoResetEvent(false);

    public event EventHandler<Request> Request;

    public int ThreadId => Message.Header.ThreadId;

    public Message Message { get; }

    public WaiterState State { get; private set; }

    public ThreadWaiter(Message message)
    {
        Message = message;
    }

    public Message Wait()
    {
        var header = Message.Header;
        while (true)
        {
            State = WaiterState.Waiting;

            // TODO move time to config
            var eventIndex = WaitHandle.WaitAny(new[] { _gotRequestEvent, _gotResponseEvent }, 1000);
            if (eventIndex == 0)
            {
                State = WaiterState.ProcessingRequest;
                while (_requests.TryDequeue(out Request newRequest))
                {
                    Request?.Invoke(this, newRequest);
                }
            }
            else if (eventIndex == 1)
            {
                if (_responses.TryRemove(header.Id, out var matchedResponse))
                {
                    State = WaiterState.Finished;
                    WriteLine($"FINISHED!!!!");
                    return matchedResponse;
                }
            }

            WriteLine($"!!!!!!!!!! Still waiting for response !!!!!!!!!");
        }
    }

    public void ReceiveRequest(Request request)
    {
        switch (request.Message.Header.MessageType)
        {
            case MessageType.Event:
            case MessageType.MethodCall:
                AddRequest(request);
                break;
            case MessageType.MethodCallResult:
            case MessageType.EventResponse:
                AddResponse(request);
                break;
            default:
                throw new NotSupportedException($"{request.Message.Header.MessageType}");
        }
    }

    private void AddRequest(Request request)
    {
        _requests.Enqueue(request);
        _gotRequestEvent.Set();
    }

    private void AddResponse(Request request)
    {
        var message = request.Message;
        if (!_responses.TryAdd(message.Header.Id, message))
        {
            throw new InvalidOperationException($"Can not add response to this waiter.");
        }

        _gotResponseEvent.Set();
    }

    private void WriteLine(string line, [CallerMemberName] string methodName = null)
    {
        var realThreadId = Thread.CurrentThread.ManagedThreadId;
        var className = GetType().Name;
        Debug.WriteLine($"(Thread: {realThreadId}) {className}.{methodName} >>> ({Message}) {line}");
    }
}
