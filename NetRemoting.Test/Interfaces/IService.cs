using System;

namespace NetRemoting.Test.Interfaces;

public interface IService
{
    IDependency Dependency { get; set; }
    Func<string, bool> Delegate { get; set; }

    event EventHandler<string> ConvertStarted;
    event EventHandler<string> ConvertEnded;

    event EventHandler<string> Ping;

    event EventHandler<DataEventArgs> Data;

    event Action Action;

    string Convert(string value);

    uint Sum(uint[] array);

    Guid Second(Guid value, bool x);

    void Out(out string x);
}
