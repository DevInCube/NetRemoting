using NetRemoting.Communication;
using System.Diagnostics;
using System.Text;

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

        if (Value == null)
        {
            return null;
        }

        if (Type.IsEnum)
        {
            return Enum.ToObject(Type, Value);
        }

        if (Type == typeof(int))
        {
            return Convert.ToInt32(Value);
        }

        if (Type == typeof(uint))
        {
            return Convert.ToUInt32(Value);
        }

        if (Type == typeof(uint[]))
        {
            return AsIntArray().Select(x => (uint)x).ToArray();
        }

        if (Type == typeof(byte[]))
        {
            if (Value is string encodedBytesString)
            {
                return Encoding.ASCII.GetBytes(encodedBytesString);
            }

            return AsIntArray().Select(x => (byte)x).ToArray();
        }

        if (Type == typeof(string) && Value is string strValue)
        {
            return strValue;
        }

        if (Value.GetType() == Type)
        {
            return Value;
        }

        return Value;
    }

    public T? As<T>()
    {
        return (T?)Value;
    }

    // TODO resolve this from data sent over network
    public Instance AsInstance()
    {
        return SerializationHelper.ParseInstance(ToString());
    }

    // TODO resolve this from data sent over network
    public int AsInt()
    {
        return Convert.ToInt32(Value);
    }

    // TODO resolve this from data sent over network
    public int[] AsIntArray()
    {
        return ((Newtonsoft.Json.Linq.JArray?)Value)?.Select(x => (int)x).ToArray() ?? [];
    }

    public T? AsEnum<T>()
        where T : Enum
    {
        if (Value is null)
        {
            return default;
        }

        if (Value is T t)
        {
            return t;
        }

        return (T)Enum.ToObject(typeof(T), Value);
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
