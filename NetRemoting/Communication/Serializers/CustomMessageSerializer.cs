namespace NetRemoting.Communication.Serializers;

public class CustomMessageSerializer : IMessageSerializer
{
    public static readonly CustomMessageSerializer Instance = new();

    public string FormatMessage(Message message) =>
        SerializationHelper.FormatMessage(message);

    public Message ParseMessage(string messageString) =>
        SerializationHelper.ParseMessage(messageString);
}
