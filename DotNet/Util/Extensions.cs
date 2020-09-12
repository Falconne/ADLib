using JetBrains.Annotations;
using System;

namespace ADLib.Util
{
    public static class Extensions
    {
        public static bool EqualsIgnoringCase(this string str, string compareTo)
        {
            return str.Equals(compareTo, StringComparison.InvariantCultureIgnoreCase);
        }

        [AssertionMethod]
        public static bool IsEmpty([AssertionCondition(AssertionConditionType.IS_NOT_NULL)] this string str)
        {
            return string.IsNullOrWhiteSpace(str);
        }

    }
}