namespace ADLib.Util;

public static class EnumerableExtensions
{
    public static bool NotAny<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
    {
        return !source.Any(predicate);
    }
}