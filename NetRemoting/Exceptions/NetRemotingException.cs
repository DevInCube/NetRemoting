namespace NetRemoting.Exceptions;

public class NetRemotingException : Exception
{
    public NetRemotingException(string? message)
        : base(message)
    {
    }
}
