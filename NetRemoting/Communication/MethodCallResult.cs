using System.Diagnostics;

namespace NetRemoting.Communication;

[DebuggerDisplay("{ToString()}")]
public class MethodCallResult
{
    public Signature Signature { get; set; }
    public ResultValue ResultValue { get; set; }
    public Object[] OutArguments { get; set; }

    public override string ToString()
    {
        return SerializationHelper.FormatMethodCallResult(this);
    }
}
