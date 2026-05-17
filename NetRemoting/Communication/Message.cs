using System;
using System.Diagnostics;

namespace NetRemoting.Communication;

[DebuggerDisplay("{ToString()}")]
public class Message
{
    public MessageHeader Header { get; }
    public object Payload { get; set; }

    public Message(MessageType messageType, object payload = null)
        : this(new MessageHeader
        {
            Id = Guid.NewGuid(),
            MessageType = messageType,
        }, payload)
    {
    }

    public Message(MessageHeader header, object payload = null)
    {
        Header = header;
        Payload = payload;
    }

    public override string ToString()
    {
        return SerializationHelper.FormatMessage(this);
    }
}
