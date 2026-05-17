using NetRemoting.Communication;
using NetRemoting.Test.Interfaces;
using System.Net.WebSockets;
using System.Text;
using WatsonWebsocket;

namespace NetRemoting.Test.Server;

public class ServerHub
{
    private const int port = 8000;

    private readonly WatsonWsServer _server;

    public ServerHub(IService service)
    {
        var mainHub = Hub.CreateMainHub();

        _ = mainHub.InstantiateSingleton<IService>(service);

        _server = new WatsonWsServer("localhost", port, ssl: false);
        _server.Logger = (data) => WriteLine(data);
        _server.ClientConnected += ClientConnected;
        _server.ClientDisconnected += ClientDisconnected;
        _server.MessageReceived += MessageReceived;

        void ClientConnected(object sender, ConnectionEventArgs args)
        {
            WriteLine($"Client connected: {args.Client}");
            _ = new Hub(mainHub, args.Client.Guid, dataBytes =>
            {
                return _server.SendAsync(args.Client.Guid, dataBytes, WebSocketMessageType.Text)
                    .GetAwaiter()
                    .GetResult();
            });
        }

        void ClientDisconnected(object sender, DisconnectionEventArgs args)
        {
            WriteLine($"Client disconnected: {args.Client}");
            // TODO remove from main hub
            //_clientHubs.TryRemove(args.Client.Guid, out _); // TODO dispose and notify callers?
        }

        void MessageReceived(object sender, MessageReceivedEventArgs args)
        {
            var messageString = Encoding.UTF8.GetString(args.Data.ToArray());
            WriteLine($"Message received from `{args.Client.Guid}`: `{messageString}`");

            var requests = SerializationHelper.ParseMessages(messageString)
                .Select(x => new Request(args.Client.Guid, x))
                .ToArray();
            foreach (var request in requests)
            {
                mainHub.ReceiveRequest(request);
            }
        }
    }

    public void StartServer()
    {
        _server.Start();
        WriteLine($"Started at {port}");
    }

    private void WriteLine(string line)
    {
        Console.WriteLine($"{nameof(ServerHub)} >>> {line}");
    }
}
