namespace Server.Tools;

internal static class TimestampSource
{
    public static long Timestamp => (long)_timeProvider.GetElapsedTime(_start).TotalMilliseconds;

    static TimestampSource()
    {
        _start = _timeProvider.GetTimestamp();
    }

    static TimeProvider _timeProvider = TimeProvider.System;
    static long _start;
}
