using Newtonsoft.Json;

namespace NetRemoting.Communication.Serializers;

public class JsonMessageSerializer : IMessageSerializer
{
    public static readonly JsonMessageSerializer Instance = new();

    private static readonly JsonSerializerSettings s_settings = new()
    {
        TypeNameHandling = TypeNameHandling.Auto,
        NullValueHandling = NullValueHandling.Include,
    };

    public string FormatMessage(Message message) =>
        JsonConvert.SerializeObject(message, s_settings);

    public Message ParseMessage(string messageString) =>
        JsonConvert.DeserializeObject<Message>(messageString, s_settings)
            ?? throw new FormatException($"Failed to deserialize message: `{messageString}`");
}
