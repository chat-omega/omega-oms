
namespace ZeroPlus.Comms.Helper.Utilities
{
    public static class Guard
    {
        public static void NotNull<T>(T value, string name) where T : class
        {
            if (value == null) throw new ArgumentNullException(name);
        }
    }
}
