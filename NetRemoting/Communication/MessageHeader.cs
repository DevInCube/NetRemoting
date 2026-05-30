using System.Diagnostics;

namespace NetRemoting.Communication;

[DebuggerDisplay("{ToString()}")]
public class MessageHeader
{
    public MessageType MessageType { get; set; }

    public Guid Id { get; set; }

    public int ThreadId { get; set; }

    public MessageHeader()
    {
        ThreadId = Thread.CurrentThread.ManagedThreadId;
    }

    public override string ToString()
    {
        return SerializationHelper.FormatMessageHeader(this);
    }
}
