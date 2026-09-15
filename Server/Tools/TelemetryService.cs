using Microsoft.Extensions.Logging;

namespace Server.Tools;

internal class TelemetryService
{
    public static Task<Common.Bool> SetLogFileName(
        string filename,
        FileLogger fileLogger,
        ILogger logger)
    {
        if (string.IsNullOrEmpty(filename))
        {
            if (fileLogger.IsLogging)
            {
                logger.LogInformation("Logging disabled");
                fileLogger.SetFileName(string.Empty);
            }
            return Task.FromResult(new Common.Bool { Value = false });
        }
        else
        {
            var result = fileLogger.SetFileName(filename);
            if (result)
                logger.LogInformation("Logging to {filename}", filename);
            else
                logger.LogWarning("Cannot log to {filename}", filename);
            return Task.FromResult(new Common.Bool { Value = result });
        }
    }
}