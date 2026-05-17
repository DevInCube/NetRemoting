namespace NetRemoting.Communication;

public class Response
{
    public Request Request { get; }

    public Message Message { get; }

    public Response(Request request, Message message)
    {
        Request = request;
        Message = message;
    }
}
