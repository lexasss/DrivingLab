using System.IO;

namespace ClientExample.Tools;

internal static class StringExt
{
    public static string ToPath(this string s, string replacement = "-")
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        string[] temp = s.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(replacement, temp);
    }

    public static bool IsValidPath(this string s)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return s.Trim().Length > 0 && s.IndexOfAny(invalidChars) < 0;
    }
}
