using JetBrains.Annotations;

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

        [AssertionMethod]
        //public static bool IsEmpty([AssertionCondition(AssertionConditionType.IS_NOT_NULL)] this string str)
        public static bool IsEmpty(this string? str)
        {
            return string.IsNullOrWhiteSpace(str);
        }

        [AssertionMethod]
        //public static bool IsNotEmpty([AssertionCondition(AssertionConditionType.IS_NOT_NULL)] this string str)
        public static bool IsNotEmpty(this string? str)
        {
            return !string.IsNullOrWhiteSpace(str);
        }

    }
}