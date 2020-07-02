using System;

namespace ADLib.Util
{
    public static class Extensions
    {
        public static bool EqualsIgnoringCase(this string str, string compareTo)
        {
            return str.Equals(compareTo, StringComparison.InvariantCultureIgnoreCase);
        }
    }
}