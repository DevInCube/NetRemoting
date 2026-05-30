using NetRemoting.Communication;
using NetRemoting.Communication.Serializers;
using NetRemoting.Test.Interfaces;
using System.Net.WebSockets;
using System.Text;
using WatsonWebsocket;

namespace NetRemoting.Test.Server;

public class ServerHub
{
    private const int port = 8000;

    private readonly WatsonWsServer _server;

    public ServerHub(IService service, IMessageSerializer? serializer = null)
    {
        serializer ??= CustomMessageSerializer.Instance;
        var mainHub = Hub.CreateMainHub(serializer);

        _ = mainHub.InstantiateSingleton<IService>(service);

        _server = new WatsonWsServer("localhost", port, ssl: false)
        {
            Logger = (data) => WriteLine(data)
        };
        _server.ClientConnected += ClientConnected;
        _server.ClientDisconnected += ClientDisconnected;
        _server.MessageReceived += MessageReceived;

        void ClientConnected(object? sender, ConnectionEventArgs args)
        {
            WriteLine($"Client connected: {args.Client}");
            _ = new Hub(mainHub, args.Client.Guid, dataBytes =>
            {
                return _server.SendAsync(args.Client.Guid, dataBytes, WebSocketMessageType.Text)
                    .GetAwaiter()
                    .GetResult();
            }, serializer);
        }

        void ClientDisconnected(object? sender, DisconnectionEventArgs args)
        {
            WriteLine($"Client disconnected: {args.Client}");
            // TODO remove from main hub
            //_clientHubs.TryRemove(args.Client.Guid, out _); // TODO dispose and notify callers?
        }

        void MessageReceived(object? sender, MessageReceivedEventArgs args)
        {
            var messageString = Encoding.UTF8.GetString(args.Data.ToArray());
            WriteLine($"[Thread {Thread.CurrentThread.ManagedThreadId}] Message received from `{args.Client.Guid}`: `{messageString}`");

            var message = serializer.ParseMessage(messageString);
            var request = new Request(args.Client.Guid, message);
            Task.Run(() => mainHub.ReceiveRequest(request));

            WriteLine($"[Thread {Thread.CurrentThread.ManagedThreadId}] MessageReceived handler RETURNING");
        }
    }

    public void StartServer()
    {
        _server.Start();
        WriteLine($"Started at {port}");
    }

    private static void WriteLine(string line)
    {
        ////Console.WriteLine($"{nameof(ServerHub)} >>> {line}");
    }
}
