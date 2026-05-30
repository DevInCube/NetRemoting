using System.Diagnostics;

namespace NetRemoting;

[DebuggerDisplay("{ToString()}")]
public class Object
{
    public required Type Type { get; init; }

    public string? Name { get; set; }

    public object? Value { get; set; }

    public object? GetValue()
    {
        if (Type == null)
        {
            return null;
        }

        return Value;
    }

    public override string ToString()
    {
        const string @null = "<null>";
        return $"{Type?.Name ?? @null} {Name ?? @null} {Value ?? @null}";
    }

    public static Object Create<T>(T argValue)
    {
        return Create(typeof(T), argValue);
    }

    public static Object Create(Type type, object? argValue, string? name = null)
    {
        return new Object
        {
            Type = type,
            Name = name,
            Value = argValue,
        };
    }
}
