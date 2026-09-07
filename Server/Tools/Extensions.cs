namespace Server.Tools;

internal static class DoubleExt
{
    public static double ToRange(this double self, double min, double max) => Math.Max(min, Math.Min(self, max));
}
