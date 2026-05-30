using NetRemoting.Test.Interfaces;
using System.Timers;

namespace NetRemoting.Test.Implementations;

public class Service : IService
{
    private readonly System.Timers.Timer _timer;

    public IDependency? Dependency { get; set; }

    public Func<string, bool>? Delegate { get; set; }

    public event EventHandler<string>? ConvertStarted;
    public event EventHandler<string>? ConvertEnded;
    public event EventHandler<string>? Ping;
    public event EventHandler<DataEventArgs>? Data;
    public event Action? Action;

    public Service()
    {
        _timer = new System.Timers.Timer();
        _timer.Elapsed += _timer_Elapsed;
        _timer.Interval = 2000;
        ////_timer.Enabled = true;
    }

    public void DoPing()
    {
        Ping?.Invoke(this, DateTimeOffset.Now.ToString());
        Action?.Invoke();
    }

    private void _timer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        DoPing();
    }

    public string Convert(string value)
    {
        Delegate?.Invoke("Convert");
        ConvertStarted?.Invoke(this, DateTimeOffset.Now.ToString());
        Dependency?.SetMe(nameof(Convert));
        ConvertEnded?.Invoke(this, DateTimeOffset.Now.ToString());
        var data = value + "!";
        Data?.Invoke(this, new DataEventArgs { Data = data });
        return data;
    }

    public Guid Second(Guid value, bool x)
    {
        _ = Delegate?.Invoke("Second");
        return x ? value : Guid.Empty;
    }

    public uint Sum(uint[] array)
    {
        _ = Delegate?.Invoke("Sum");
        uint s = 0;
        foreach (var item in array)
        {
            s += item;
        }

        return s;
    }

    public void Out(out string x)
    {
        x = "This is out value.";
    }
}
