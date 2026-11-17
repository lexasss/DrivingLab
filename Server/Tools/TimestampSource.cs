namespace Server.Tools;

internal static class TimestampSource
{
    public static long Timestamp => (long)_timeProvider.GetElapsedTime(_start).TotalMilliseconds;

    #region Internal

    static readonly TimeProvider _timeProvider = TimeProvider.System;
    static readonly long _start;

    static TimestampSource()
    {
        _start = _timeProvider.GetTimestamp();
    }

    #endregion
}
