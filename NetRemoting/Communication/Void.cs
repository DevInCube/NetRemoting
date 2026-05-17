using NetRemoting.Communication;
using System;

namespace NetRemoting;

public class Return
{
    private static Object s_void;

    public static Object Void => s_void ?? (s_void = Object.Create(typeof(void), null));

    [Obsolete]
    public static Return<T> Create<T>(T value)
    {
        return new Return<T> { Value = value };
    }
}

[Obsolete]
public class Return<T>
{
    public T Value { get; set; }

    public static implicit operator T(Return<T> d) => d.Value;

    public Instance AsInstance()
    {
        return SerializationHelper.ParseInstance(Value.ToString());
    }
}
