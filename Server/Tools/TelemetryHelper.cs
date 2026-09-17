using Microsoft.Extensions.Logging;

namespace Server.Tools;

internal class TelemetryHelper
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

            return Common.Bool.False;
        }
        else
        {
            var result = fileLogger.SetFileName(filename);

            if (result)
                logger.LogInformation("Logging to {filename}", filename);
            else
                logger.LogWarning("Cannot log to {filename}", filename);

            return Common.Bool.From(result);
        }
    }
}