namespace NetRemoting;

public static class Return
{
    private static Object? s_void;

    public static Object Void => s_void ??= Object.Create(typeof(void), null);
}
