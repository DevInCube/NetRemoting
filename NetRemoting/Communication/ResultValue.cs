using System;

namespace NetRemoting.Communication;

public class ResultValue
{
    public static ResultValue Void { get; } = new ResultValue { IsVoid = true };

    public static ResultValue CreateResult(Object result) => new ResultValue { Result = result };

    public static ResultValue CreateException(Exception exception) => new ResultValue { Exception = exception };

    public bool IsVoid { get; set; }

    public Object Result { get; set; }

    public Exception Exception { get; set; }
}
