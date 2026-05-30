using RemotingObject = NetRemoting.Object;

namespace NetRemoting.Communication;

internal static class DisplayFormatter
{
    private const string Void = "void";
    private const string Null = "<null>";

    public static string FormatInstance(Instance instance)
    {
        return instance.InstanceId != null
            ? $"{instance.ServiceName}@{instance.InstanceId}"
            : instance.ServiceName;
    }

    public static string FormatMessageHeader(MessageHeader header)
    {
        return $"[{header.MessageType}] {header.Id} Thread:{header.ThreadId}";
    }

    public static string FormatSignature(Signature signature)
    {
        return signature.InstanceId != null
            ? $"{signature.ServiceName}@{signature.InstanceId}.{signature.MethodName}"
            : $"{signature.ServiceName}.{signature.MethodName}";
    }

    public static string FormatMethodCall(MethodCall call)
    {
        var args = string.Join(", ", call.Arguments.Select(FormatObject));
        return $"{FormatSignature(call.Signature)}({args})";
    }

    public static string FormatMethodCallResult(MethodCallResult result)
    {
        if (result.ResultValue.IsVoid)
        {
            return Void;
        }

        var resultPart = result.ResultValue.Exception != null
            ? $"exception: {result.ResultValue.Exception.Message}"
            : FormatObject(result.ResultValue.Result);
        return $"{FormatSignature(result.Signature)} → {resultPart}";
    }

    public static string FormatMessage(Message message)
    {
        return $"{FormatMessageHeader(message.Header)}: {message.Payload}";
    }

    private static string FormatObject(RemotingObject? obj)
    {
        if (obj is null)
        {
            return Null;
        }

        return obj.Value?.ToString() ?? Null;
    }
}
