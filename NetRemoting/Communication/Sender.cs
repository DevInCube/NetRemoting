namespace NetRemoting.Communication
{
    internal static class Sender
    {
        private static readonly Type Type = typeof(Sender);
        public const string Value = "<sender>";

        public static bool IsSender(Object @object)
        {
            return @object.Type == Type;
        }

        /// <summary>
        /// Creates a reference sender Object that will be sent during communication.
        /// </summary>
        public static Object CreateRef(string? name = null)
        {
            return Object.Create(Type, Value, name);
        }

        /// <summary>
        /// Creates a sender object with a sender resolved value.
        /// </summary>
        public static Object CreateVal(object sender)
        {
            return Object.Create(Type, argValue: sender);
        }
    }
}
