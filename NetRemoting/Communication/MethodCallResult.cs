using System.Diagnostics;

namespace NetRemoting.Communication;

[DebuggerDisplay("{ToString()}")]
public class MethodCallResult
{
    public required Signature Signature { get; set; }

    public required ResultValue ResultValue { get; set; }

    public required Object[] OutArguments { get; set; }

    public override string ToString()
    {
        return SerializationHelper.FormatMethodCallResult(this);
    }
}
