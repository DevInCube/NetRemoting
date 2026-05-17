using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace NetRemoting.Communication;

public static class SerializationHelper
{
    private static string Null = "<null>";
    private static string Sender = "<sender>";

    public static string FormatInstance(Instance instance)
    {
        var instancePart = instance.InstanceId != null
            ? $"@{instance.InstanceId}"
            : string.Empty;
        return $"{instance.ServiceName}{instancePart}";
    }

    public static Instance ParseInstance(string instanceString)
    {
        var serviceNameParts = instanceString.Split('@');
        var serviceName = serviceNameParts[0];
        if (string.IsNullOrWhiteSpace(serviceName))
            throw new FormatException("Service name can not be empty.");

        var instanceId = serviceNameParts.Length > 1
            ? Guid.Parse(serviceNameParts[1])
            : (Guid?)null;
        return new Instance(serviceName, instanceId);
    }

    public static bool TryParseInstance(string arg, out Instance instance)
    {
        if (arg.Contains('@') && Guid.TryParse(arg.Split('@')[1], out _))
        {
            instance = ParseInstance(arg);
            return true;
        }

        instance = null;
        return false;
    }

    private static string s_pattern = @"\),\(";
    private static Regex s_argumentsSplitRegex = new Regex(s_pattern);

    private static Object[] ParseArguments(string part)
    {
        var arguments = s_argumentsSplitRegex.Split(part.Trim().TrimStart('(').TrimEnd(')'))
            .Where(x => !string.IsNullOrEmpty(x))  // TODO passing empty strings as arguments
            .Select(ParseArgument)
            .ToArray();
        return arguments;
    }

    private static Object ParseArgument(string arg)
    {
        // TODO empty strings
        if (string.IsNullOrEmpty(arg))
            throw new ArgumentException(nameof(arg));

        if (arg == Null)
            return null;

        if (arg == Sender)
            return Object.Create(null, arg);

        try
        {
            var argument = JsonConvert.DeserializeObject<Object>(arg);
            return argument;
        }
        catch (Exception)
        {

        }

        throw new NotImplementedException();
    }

    private static string FormatArguments(Object[] args)
    {
        // TODO make sure that there are no commas in arguments
        // Serialize complex arguments in JSON
        var arguments = args?.Select(FormatArgument).ToArray();
        var argumentsPart = arguments == null
            ? string.Empty
            : string.Join("),(", arguments);
        return argumentsPart;
    }

    public static string[] ProcessArguments(Object[] args)
    {
        return args?
            .Select(FormatArgument)
            .ToArray();
    }

    public static string FormatArgument(Object argument)
    {
        if (argument == null)
            throw new ArgumentNullException(nameof(argument));

        // Special values.
        if (argument.Type == null)
        {
            if (argument.Value == null)
                return Null;

            return argument.Value.ToString();
        }

        return JsonConvert.SerializeObject(argument);
    }

    public static MethodCall ParseMethodCall(string callString)
    {
        var callParts = callString.Split('|');
        if (callParts.Length != 2)
            throw new FormatException($"Invalid method call format: `{callString}`");

        var signature = ParseSignature(callParts[0]);
        var arguments = ParseArguments(callParts[1]);
        return new MethodCall(signature, arguments);
    }

    public static EventCall ParseEventCall(string callString)
    {
        var callParts = callString.Split('|');
        if (callParts.Length != 2)
            throw new FormatException($"Invalid event call format: `{callString}`");

        var signature = ParseSignature(callParts[0]);
        var arguments = ParseArguments(callParts[1]);
        return new EventCall(signature, arguments);
    }

    private static Signature ParseSignature(string signature)
    {
        var nameParts = signature.Split('.');
        var serviceNameParts = nameParts[0].Split('@');
        var serviceName = serviceNameParts[0];
        var instanceId = serviceNameParts.Length > 1 ? serviceNameParts[1] : null;
        var methodName = nameParts[1];

        return new Signature
        {
            ServiceName = serviceName,
            InstanceId = instanceId == null ? (Guid?)null : Guid.Parse(instanceId),
            MethodName = methodName,
        };
    }

    public static MethodCallResult ParseCallResult(string callString)
    {
        var callParts = callString.Split('|');
        if (callParts.Length != 3)
            throw new FormatException($"Invalid call result format: `{callString}`");

        var signature = ParseSignature(callParts[0]);
        var resultValue = ParseResultValue(callParts[1]);
        var outArguments = ParseOutArguments(callParts[2]);
        return new MethodCallResult
        {
            Signature = signature,
            ResultValue = resultValue,
            OutArguments = outArguments,
        };
    }

    public static EventResponse ParseEventResponse(string callString)
    {
        var callParts = callString.Split('|');
        if (callParts.Length != 2)
            throw new FormatException($"Invalid event response format: `{callString}`");

        if (!string.IsNullOrEmpty(callParts[1]))
            throw new FormatException($"Invalid event response format: second part should be empty.");

        var signature = ParseSignature(callParts[0]);
        return new EventResponse
        {
            Signature = signature,
        };
    }

    public static string FormatSignature(Signature signature)
    {
        var instancePart = signature.InstanceId != null
            ? $"@{signature.InstanceId}"
            : string.Empty;
        return $"{signature.ServiceName}{instancePart}.{signature.MethodName}";
    }

    public static string FormatMethodCall(MethodCall call)
    {
        var signaturePart = FormatSignature(call.Signature);
        var argumentsPart = FormatArguments(call.Arguments);
        return $"{signaturePart}|({argumentsPart})";
    }

    public static string FormatEventCall(EventCall call)
    {
        return FormatMethodCall(call);
    }

    public static string FormatMessageHeader(MessageHeader header)
    {
        return $"{header.MessageType}:{header.Id}:{header.ThreadId}";
    }

    private static MessageHeader ParseMessageHeader(string header)
    {
        var parts = header.Split(':');
        if (parts.Length != 3)
            throw new FormatException($"Invalid message header: `{header}`.");

        if (!Enum.TryParse(parts[0], out MessageType messageType))
            throw new NotSupportedException($"Message type: `{parts[0]}`.");

        if (!Guid.TryParse(parts[1], out Guid guid))
            throw new FormatException($"Invalid message header id: `{parts[1]}`.");

        if (!int.TryParse(parts[2], out int number))
            throw new FormatException($"Invalid message header thread id: `{parts[2]}`.");

        return new MessageHeader
        {
            MessageType = messageType,
            Id = guid,
            ThreadId = number,
        };
    }

    private static string FormatOutArguments(Object[] outArguments)
    {
        var argumentsPart = FormatArguments(outArguments);
        return $"Out({argumentsPart})";
    }

    private static string FormatResultValue(ResultValue resultValue)
    {
        if (resultValue.IsVoid)
            return nameof(ResultValueType.Void);

        if (resultValue.Exception != null)
            return $"{nameof(ResultValueType.Exception)}({resultValue.Exception})";

        return $"{nameof(ResultValueType.Result)}({JsonConvert.SerializeObject(resultValue.Result)})";
    }

    private static ResultValue ParseResultValue(string str)
    {
        var parts = str.Split('(');
        var part1 = parts.Length > 1 ? string.Join("(", parts.Skip(1)).TrimEnd(')') : null;
        switch (parts[0])
        {
            case nameof(ResultValueType.Void): return ResultValue.Void;
            case nameof(ResultValueType.Exception): return ResultValue.CreateException(new Exception(part1));
            case nameof(ResultValueType.Result): return ResultValue.CreateResult(JsonConvert.DeserializeObject<Object>(part1));
            default: throw new NotSupportedException(parts[0]);
        }
    }

    private static Object[] ParseOutArguments(string outArgumentsPart)
    {
        var startIndex = "Out(".Length;
        var endIndex = outArgumentsPart.Length - startIndex - ")".Length;
        var argumentsPart = outArgumentsPart.Substring(startIndex, endIndex);
        return ParseArguments(argumentsPart);
    }

    public static string FormatMethodCallResult(MethodCallResult callResult)
    {
        var signaturePart = FormatSignature(callResult.Signature);
        var resultValuePart = FormatResultValue(callResult.ResultValue);
        var outArgumentsPart = FormatOutArguments(callResult.OutArguments);
        return $"{signaturePart}|{resultValuePart}|{outArgumentsPart}";
    }

    public static string FormatEventResponse(EventResponse callResult)
    {
        var signaturePart = FormatSignature(callResult.Signature);
        return $"{signaturePart}|";
    }

    public static Message ParseMessage(string message)
    {
        var parts = message.Split('#');
        if (parts.Length != 2)
            throw new FormatException($"Invalid message: `{message}`.");

        var header = ParseMessageHeader(parts[0]);
        var payload = ParsePayload(header.MessageType, parts[1]);
        return new Message(header)
        {
            Payload = payload,
        };
    }

    public static IEnumerable<Message> ParseMessages(string text)
    {
        return text.Split(';')  // messages separator
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Where(x => !x.StartsWith("//"))  // comments
            .Select(ParseMessage)
            .ToArray();
    }

    private static object ParsePayload(MessageType messageType, string payloadString)
    {
        switch (messageType)
        {
            case MessageType.MethodCall: return ParseMethodCall(payloadString);
            case MessageType.MethodCallResult: return ParseCallResult(payloadString);
            case MessageType.Event: return ParseEventCall(payloadString);
            case MessageType.EventResponse: return ParseEventResponse(payloadString);
            default:
                throw new NotSupportedException(messageType.ToString());
        }
    }

    public static string FormatMessage(Message message)
    {
        var header = FormatMessageHeader(message.Header);
        var payloadPart = FormatPayload(message.Header.MessageType, message.Payload);
        return $"{header}#{payloadPart}";
    }

    private static string FormatPayload(MessageType messageType, object payload)
    {
        switch (messageType)
        {
            case MessageType.MethodCall when payload is MethodCall call: return FormatMethodCall(call);
            case MessageType.MethodCallResult when payload is MethodCallResult callResult: return FormatMethodCallResult(callResult);
            case MessageType.Event when payload is EventCall eventCall: return FormatEventCall(eventCall);
            case MessageType.EventResponse when payload is EventResponse eventResponse: return FormatEventResponse(eventResponse);
            default:
                throw new NotSupportedException(messageType.ToString());
        }
    }

    private enum ResultValueType
    {
        Void,
        Exception,
        Result,
    }
}
