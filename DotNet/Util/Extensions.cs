using System.Diagnostics.CodeAnalysis;

namespace ADLib.Util
{
    public static class Extensions
    {
        public static bool ContainsIgnoringCase(this string str, string compareTo)
        {
            return str.ToLower().Contains(compareTo.ToLower());
        }

        public static bool EqualsIgnoringCase(this string str, string compareTo)
        {
            return str.Equals(compareTo, StringComparison.InvariantCultureIgnoreCase);
        }

        public static bool IsEmpty([NotNullWhen(false)] this string? str)
        {
            return string.IsNullOrWhiteSpace(str);
        }

        public static bool IsNotEmpty([NotNullWhen(true)] this string? str)
        {
            return !string.IsNullOrWhiteSpace(str);
        }

        public static IEnumerable<(T item, int index)> WithIndex<T>(this IEnumerable<T> source)
        {
            return source.Select((item, index) => (item, index));
        }
    }
}