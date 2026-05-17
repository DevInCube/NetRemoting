using System;

namespace NetRemoting;

public interface ICallConfiguration
{
    Object Call(Type type, params Object[] args);
}
