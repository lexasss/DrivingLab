using System.IO;
using System.Numerics;

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

internal static class NumExt
{
    public static T Wrap<T>(this T value, T min, T max)
        where T :INumber<T>
    {
        T range = max - min;
        while (value > max)
            value -= range;
        while (value < min)
            value += range;
        return value;
    }
}
