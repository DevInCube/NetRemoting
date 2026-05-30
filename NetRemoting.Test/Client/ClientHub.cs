using NetRemoting.Communication;
using System.Text;
using WatsonWebsocket;
using System.Net.WebSockets;

namespace NetRemoting.Test.Client;

internal sealed class ClientHub : IDisposable
{
    private readonly WatsonWsClient _client;

    public Hub Hub { get; }

    public ClientHub()
    {
        _client = new WatsonWsClient("localhost", 8000, ssl: false);

        _client.ServerConnected += ServerConnected;
        _client.ServerDisconnected += ServerDisconnected;
        _client.MessageReceived += MessageReceived;

        Hub = new Hub(null, Guid.NewGuid(), dataBytes =>
        {
            return _client.SendAsync(dataBytes, WebSocketMessageType.Text)
                .GetAwaiter()
                .GetResult();
        });

        void MessageReceived(object? sender, MessageReceivedEventArgs args)
        {
            if (Hub is null)
            {
                throw new InvalidOperationException("Received message while hub is not initialized.");
            }

            var messageString = Encoding.UTF8.GetString([.. args.Data]);
            WriteLine($"[Thread {Thread.CurrentThread.ManagedThreadId}] Message from server: `{messageString}`.");

            var message = SerializationHelper.ParseMessage(messageString);
            Hub.ReceiveRequest(new Request(Hub.ClientId, message));
            WriteLine($"[Thread {Thread.CurrentThread.ManagedThreadId}] MessageReceived handler RETURNING");
        }

        void ServerConnected(object? sender, EventArgs args)
        {
            WriteLine($"Server connected.");
        }

        void ServerDisconnected(object? sender, EventArgs args)
        {
            WriteLine($"Server disconnected.");
        }
    }

    public void Dispose()
    {
        DisconnectFromServer();
    }

    public void ConnectToServer()
    {
        WriteLine($"{nameof(ConnectToServer)}.");
        _client.Start();
    }

    public void DisconnectFromServer()
    {
        WriteLine($"{nameof(DisconnectFromServer)}.");
        try
        {
            _client.Stop();
        }
        catch (ObjectDisposedException)
        {
            // Ignore.
        }
    }

    private static void WriteLine(string line)
    {
        ////Console.WriteLine($"{nameof(ClientHub)} >>> {line}");
    }
}
