namespace NetRemoting.Communication;

public interface IMessageSerializer
{
    string FormatMessage(Message message);

    Message ParseMessage(string messageString);
}
