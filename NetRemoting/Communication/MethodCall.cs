using System.Diagnostics;

namespace NetRemoting.Communication;

[DebuggerDisplay("{ToString()}")]
public class MethodCall
{
    public Signature Signature { get; }

    public Object[] Arguments { get; }

    public MethodCall(Signature signature, Object[] arguments)
    {
        Signature = signature;
        Arguments = arguments;
    }

    public override string ToString()
    {
        return DisplayFormatter.FormatMethodCall(this);
    }
}
