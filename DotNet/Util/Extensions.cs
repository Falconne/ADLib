using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace ADLib.Util;

public static class Extensions
{
    public static bool ContainsIgnoringCase(this string? str, string compareTo)
    {
        return str.ToLower().Contains(compareTo.ToLower());
    }

    public static bool EqualsIgnoringCase(this string str, string compareTo)
    {
        return str.Equals(compareTo, StringComparison.InvariantCultureIgnoreCase);
    }

    public static bool EqualsIgnoringCaseAndSpace(this string str, string compareTo)
    {
        return str.Trim().Equals(compareTo.Trim(), StringComparison.InvariantCultureIgnoreCase);
    }

    public static bool IsEmpty([NotNullWhen(false)] this string? str)
    {
        return string.IsNullOrWhiteSpace(str);
    }

    public static bool IsNotEmpty([NotNullWhen(true)] this string? str)
    {
        return !string.IsNullOrWhiteSpace(str);
    }

    public static bool IsASCII(this string value)
    {
        // ASCII encoding replaces non-ascii with question marks, so we use UTF8 to see if multi-byte sequences are there
        return Encoding.UTF8.GetByteCount(value) == value.Length;
    }

    public static IEnumerable<(T item, int index)> WithIndex<T>(this IEnumerable<T> source)
    {
        return source.Select((item, index) => (item, index));
    }

    public static bool IsEmpty([NotNullWhen(false)] this Array? array)
    {
        return array == null || array.Length == 0;
    }

    public static bool IsNotEmpty([NotNullWhen(true)] this Array? array)
    {
        return array is { Length: > 0 };
    }
}