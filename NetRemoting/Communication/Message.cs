using System.Diagnostics;

namespace NetRemoting.Communication;

[DebuggerDisplay("{ToString()}")]
public class Message
{
    public MessageHeader Header { get; }

    public object? Payload { get; set; }

    public Message(MessageHeader header, object? payload = null)
    {
        Header = header;
        Payload = payload;
    }

    public override string ToString()
    {
        return DisplayFormatter.FormatMessage(this);
    }

    public static Message Create(MessageType messageType, object? payload = null)
    {
        var header = new MessageHeader
        {
            Id = Guid.NewGuid(),
            MessageType = messageType,
        };
        return new(header, payload);
    }
}
