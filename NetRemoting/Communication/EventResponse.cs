namespace NetRemoting.Communication;

public class EventResponse
{
    public required Signature Signature { get; init; }

    public static EventResponse Create(Signature signature)
    {
        return new EventResponse
        {
            Signature = signature,
        };
    }
}
