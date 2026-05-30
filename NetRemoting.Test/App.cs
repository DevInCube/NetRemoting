using NetRemoting.Communication;
using NetRemoting.Communication.Serializers;
using NetRemoting.Test.Client;
using NetRemoting.Test.Implementations;
using NetRemoting.Test.Interfaces;
using NetRemoting.Test.Server;
using System.Diagnostics;

namespace NetRemoting.Test;

internal static class App
{
    public static void Main()
    {
        Parallel.Invoke(
            Service,
            //ProxyClient
            ThreadCaseClient
            //SimpleClient,
            //RandomClient,
            //RandomClient
            //RandomClient,
            //Pinger
            );
    }

    private static readonly IMessageSerializer s_serializer = CustomMessageSerializer.Instance;

    private static void Service()
    {
        var hub = new ServerHub(new Service(), s_serializer);
        hub.StartServer();

        ////Thread.Sleep(3000);
        ////service.Ping();
    }

    private static void Pinger()
    {
        while (true)
        {
            Thread.Sleep(1000);
            Console.WriteLine("ping.");
        }
    }

    private static void ProxyClient()
    {
        var client = new ClientHub(s_serializer);
        client.ConnectToServer();

        var actor = RemoteObject.For<IService>(client.Hub.CreateCallerFor<IService>());

        actor.Ping += Actor_Ping;
        actor.Action += Actor_Action;

        while (true)
        {
            Thread.Sleep(1000);
        }
    }

    private static void Actor_Action()
    {
        Console.WriteLine("action()");
    }

    private static void SimpleClient()
    {
        var client = new ClientHub(s_serializer);
        client.ConnectToServer();

        var actor = RemoteObject.For<IService>(client.Hub.CreateCallerFor<IService>());
        actor.Ping += Actor_Ping;

        while (true)
        {
            Thread.Sleep(1000);
        }
    }

    private static void ThreadCaseClient()
    {
        var client = new ClientHub(s_serializer);
        client.ConnectToServer();

        var actor = RemoteObject.For<IService>(client.Hub.CreateCallerFor<IService>());

        actor.Data += Actor_Data;
        actor.Ping += Actor_Ping;
        actor.Action += Actor_Action;
        actor.ConvertStarted += Actor_ConvertStarted;
        actor.ConvertEnded += Actor_ConvertEnded;

        actor.Delegate = msg => msg.Length > 5;

        var initialDep = new Dependency();
        actor.Dependency = initialDep;
        var dep = actor.Dependency;
        // Feature: no need to create new remote if we own referenced object.
        Debug.Assert(dep == initialDep);
        Console.WriteLine($"dep check: {dep}");

        Console.WriteLine($"sum: {actor.Sum(new uint[] { 1, 2, 3 })}");

        actor.Out(out var outVar);
        Console.WriteLine($"out parameter: {outVar}");

        while (true)
        {
            Thread.Sleep(1000);
            Console.WriteLine($"{Thread.CurrentThread.ManagedThreadId} Converted: {actor.Convert("toConvert")}");
        }
    }

    private static void Actor_Data(object? sender, DataEventArgs e)
    {
        Console.WriteLine($"Data: {e.Data}");
    }

    private static void RandomClient()
    {
        var random = new Random();

        var client = new ClientHub(s_serializer);
        client.ConnectToServer();

        var actor = RemoteObject.For<IService>(client.Hub.CreateCallerFor<IService>());
        actor.Ping += Actor_Ping;
        actor.ConvertStarted += Actor_ConvertStarted;
        actor.ConvertEnded += Actor_ConvertEnded;

        while (true)
        {
            Thread.Sleep(100 + random.Next(10) * 500);
            ////Thread.Sleep(1000);
            Console.WriteLine($"Converted: {actor.Convert("toConvert")}");
        }
    }

    private static void Actor_ConvertEnded(object? sender, string e)
    {
        Console.WriteLine($"Actor_ConvertEnded! {e}");
    }

    private static void Actor_ConvertStarted(object? sender, string e)
    {
        Console.WriteLine($"Actor_ConvertStarted! {e}");

        if (sender is not IService actor)
        {
            return;
        }

        Console.WriteLine($"{Thread.CurrentThread.ManagedThreadId} Second: {actor.Second(Guid.NewGuid(), false)}");
        Console.WriteLine($"{Thread.CurrentThread.ManagedThreadId} Second: {actor.Second(Guid.NewGuid(), true)}");
        Console.WriteLine($"{Thread.CurrentThread.ManagedThreadId} Second: {actor.Second(Guid.NewGuid(), false)}");
    }

    private static void Actor_Ping(object? sender, string e)
    {
        Console.WriteLine($"Actor_Ping! {e}");
    }
}
