using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;
using Channel = System.Threading.Channels.Channel;

namespace Server.Tools;

internal class TelemetryService<T, U> : Service<U>
    where T : Common.ILoggable
{
    public bool IsSending { get; private set; } = false;

    public TelemetryService(ILogger logger) : base(logger) { }

    public void Publish(T data)
    {
        _channel.Writer.TryWrite(data);
    }

    public void Start()
    {
        if (!IsSending)
        {
            _logger.LogInformation("Data streaming started");
            IsSending = true;
        }
    }

    public void Stop()
    {
        if (IsSending)
        {
            _logger.LogInformation("Data streaming stopped");
            IsSending = false;
        }
    }

    public Task<Common.Bool> SetLogFileName(string name)
    {
        return TelemetryHelper.SetLogFileName(name, _fileLogger, _logger);
    }

    public async Task ReadData(
        Empty request,
        IServerStreamWriter<T> responseStream,
        ServerCallContext context,
        int loggingDecimals)
    {
        if (_isReading)
            return;

        _logger.LogInformation("Data reading: started");
        _isReading = true;

        try
        {
            await foreach (var data in _channel.Reader.ReadAllAsync(context.CancellationToken))
            {
                if (IsSending)
                {
                    await responseStream.WriteAsync(data);
                    _fileLogger.Add(data.ToStringArray(loggingDecimals));
                }
            }
        }
        catch (Exception)
        { }
        finally
        {
            _logger.LogInformation("Data reading: stopped");
            _isReading = false;
        }
    }

    public override void Dispose()
    {
        _fileLogger.Dispose();

        base.Dispose();
    }

    #region Internal

    readonly Channel<T> _channel = Channel.CreateUnbounded<T>();
    readonly FileLogger _fileLogger = new();

    bool _isReading = false;

    #endregion
}
