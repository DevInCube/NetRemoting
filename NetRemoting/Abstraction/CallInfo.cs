using NetRemoting.CSharp;

namespace NetRemoting;

public class CallInfo
{
    public Hub? Hub { get; set; }

    public int ThreadId { get; set; }

    public MethodType MethodType { get; }

    public string Name { get; set; }

    public Object[] Arguments { get; set; }

    internal CallInfo(int threadId, string name, params Object[] args)
    {
        ThreadId = threadId;
        (MethodType, Name) = GetMethodTypeAndName(name);
        Arguments = args;
    }

    public static CallInfo Create(int threadId, string name, params Object[] args)
    {
        return new CallInfo(threadId, name, args);
    }

    private static (MethodType, string) GetMethodTypeAndName(string fullMethodName)
    {
        if (fullMethodName.StartsWith(Language.GetPrefix))
            return (MethodType.PropertyGet, fullMethodName.Substring(Language.GetPrefix.Length));

        if (fullMethodName.StartsWith(Language.SetPrefix))
            return (MethodType.PropertySet, fullMethodName.Substring(Language.SetPrefix.Length));

        if (fullMethodName.StartsWith(Language.AddPrefix))
            return (MethodType.EventAdd, fullMethodName.Substring(Language.AddPrefix.Length));

        if (fullMethodName.StartsWith(Language.RemovePrefix))
            return (MethodType.EventRemove, fullMethodName.Substring(Language.RemovePrefix.Length));

        return (MethodType.Default, fullMethodName);
    }
}
