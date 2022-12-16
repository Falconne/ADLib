using System.Text.RegularExpressions;

namespace ADLib.Util;

public static class StringUtils
{
    private static readonly Regex _asciiFilter = new(@"[^\u0000-\u007F]");

    public static string GetWithoutNonASCIIChars(string input)
    {
        return _asciiFilter.Replace(input, " ");
    }

}