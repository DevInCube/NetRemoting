namespace NetRemoting.Communication
{
    internal static class Null
    {
        private static readonly Type Type = typeof(Null);
        public const string Value = "<null>";

        public static Object Instance { get; } = Object.Create(Type, Value);

        public static bool IsNull(Object @object)
        {
            return @object.Type == Type;
        }
    }
}
