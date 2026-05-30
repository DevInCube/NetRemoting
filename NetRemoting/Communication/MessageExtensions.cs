namespace NetRemoting.Communication;

public static class MessageExtensions
{
    private static Message CreateReturn(this Message message, ResultValue resultValue, Object[] outArguments)
    {
        if (message.Header.MessageType != MessageType.MethodCall)
        {
            throw new InvalidOperationException($"Can return only for `{nameof(MessageType.MethodCall)}` got `{message.Header.MessageType}`.");
        }

        if (message.Payload is not MethodCall call)
        {
            throw new InvalidDataException("Message payload is not a method call.");
        }

        MessageHeader header = message.Header.CreateResponseHeader();
        MethodCallResult callResult = new()
        {
            Signature = call.Signature,
            ResultValue = resultValue,
            OutArguments = outArguments,
        };
        return new Message(header, callResult);
    }

    public static Message CreateReturn(this Message message, Object result, Object[] outArguments)
    {
        return CreateReturn(message, ResultValue.CreateResult(result), outArguments);
    }

    public static Message CreateReturnVoid(this Message message, Object[] outArguments)
    {
        return CreateReturn(message, ResultValue.Void, outArguments);
    }

    public static Message CreateThrowException(this Message message, Exception exception)
    {
        return CreateReturn(message, ResultValue.CreateException(exception), []);
    }

    public static Message CreateEventResponse(this Message message)
    {
        if (message.Header.MessageType != MessageType.Event)
        {
            throw new InvalidOperationException($"Can return only for `{nameof(MessageType.Event)}` got `{message.Header.MessageType}`.");
        }

        if (message.Payload is not MethodCall call)
        {
            throw new InvalidDataException("Message payload is not a method call.");
        }

        return new Message(message.Header.CreateResponseHeader(), EventResponse.Create(call.Signature));
    }

    public static MessageHeader CreateResponseHeader(this MessageHeader header)
    {
        if (header.MessageType == MessageType.MethodCallResult ||
            header.MessageType == MessageType.EventResponse)
        {
            throw new ArgumentException($"Can not create response header for response header {header.MessageType}.");
        }

        return new MessageHeader
        {
            Id = header.Id,
            ThreadId = header.ThreadId,
            MessageType = header.MessageType == MessageType.MethodCall
                ? MessageType.MethodCallResult
                : MessageType.EventResponse,
        };
    }
}
