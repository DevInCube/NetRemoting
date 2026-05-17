namespace NetRemoting.Communication;

public class EventResponse
{
    public Signature Signature { get; set; }

    public static EventResponse Create(Signature signature)
    {
        return new EventResponse
        {
            Signature = signature,
        };
    }
}
