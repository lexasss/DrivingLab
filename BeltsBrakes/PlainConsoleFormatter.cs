using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace BeltsBrakes;

public sealed class PlainConsoleFormatter : ConsoleFormatter
{
    public PlainConsoleFormatter() : base("plain")
    {
    }

    public override void Write<TState>(
        in LogEntry<TState> logEntry,
        IExternalScopeProvider? scopeProvider,
        TextWriter textWriter)
    {
        var message = logEntry.Formatter(
            logEntry.State,
            logEntry.Exception);

        if (!string.IsNullOrEmpty(message))
        {
            
            if (logEntry.LogLevel == LogLevel.Warning)
                textWriter.WriteLine($"{ColorYellow}{logEntry.LogLevel}{ColorReset} {message}");
            else if (logEntry.LogLevel == LogLevel.Error)
                textWriter.WriteLine($"{ColorRed}{logEntry.LogLevel}{ColorReset} {message}");
            else if (logEntry.LogLevel == LogLevel.Critical)
                textWriter.WriteLine($"{ColorMagenta}{logEntry.LogLevel}{ColorReset} {message}");
            else
                textWriter.WriteLine(message);
        }

        if (logEntry.Exception is not null)
            textWriter.WriteLine($"{ColorBold}{ColorRed}{logEntry.Exception.Message}{ColorReset}");
    }

    const string ColorReset = "\x1b[0m";
    const string ColorBold = "\x1b[1m";
    const string ColorBlack = "\x1b[30m";
    const string ColorRed = "\x1b[31m";
    const string ColorGreen = "\x1b[32m";
    const string ColorYellow = "\x1b[33m";
    const string ColorBlue = "\x1b[34m";
    const string ColorMagenta = "\x1b[35m";
    const string ColorCyan = "\x1b[36m";
    const string ColorGray = "\x1b[37m";
}