using System;

namespace NetRemoting.Communication;

public class Request
{
    public Guid ClientId { get; }

    public Message Message { get; }

    public Request(Guid clientId, Message message)
    {
        ClientId = clientId;
        Message = message;
    }

    public Response ResponseWith(Message message)
    {
        return new Response(this, message);
    }
}
