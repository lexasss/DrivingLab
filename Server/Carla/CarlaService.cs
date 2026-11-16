using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Proto = global::Carla;

namespace Server.Carla;

internal class CarlaService : 
    Proto.Dispatcher.DispatcherBase,
    ITelemetryService
{
    public bool IsAvailable() => true;

    public CarlaService(ILoggerFactory loggerFactory) : base()
    {
        _logger = loggerFactory.CreateLogger("CARL");
        _logger.LogInformation("Running");

        _baseService = new(_logger);

        StartListener();
    }

    public void Dispose()
    {
        _baseService?.Dispose();

        GC.SuppressFinalize(this);
    }

    public override Task<Common.Bool> IsAvailable(
        Empty request,
        ServerCallContext context)
    {
        return Common.Awaitable.From(IsAvailable());
    }

    public override Task<Common.Bool> IsStreamingData(
        Empty request,
        ServerCallContext context)
    {
        return Common.Awaitable.From(_baseService?.IsSending == true);
    }

    public override Task<Common.Bool> IsLogging(
        Empty request,
        ServerCallContext context)
    {
        return Common.Awaitable.From(_baseService?.IsLogging == true);
    }

    public override async Task ReadData(
        Empty request,
        IServerStreamWriter<Proto.Data> responseStream,
        ServerCallContext context)
    {
        if (_baseService == null)
            return;

        await _baseService.ReadData(request, responseStream, context, 3);
    }

    public override async Task ReadEvents(
        Empty request,
        IServerStreamWriter<Proto.Event> responseStream,
        ServerCallContext context)
    {
        if (_baseService == null)
            return;

        await _baseService.ReadEvents(request, responseStream, context);
    }


    public override Task<Empty> Start(
        Empty request,
        ServerCallContext context)
    {
        if (_baseService?.IsSending == false)
        {
            _baseService.Start();
        }
        return Common.Awaitable.Empty;
    }

    public override Task<Empty> Stop(
        Empty request,
        ServerCallContext context)
    {
        _baseService?.Stop();
        return Common.Awaitable.Empty;
    }

    public override Task<Common.Bool> SetLogFileName(
        Common.String request,
        ServerCallContext context)
    {
        if (_baseService == null)
            return Common.Awaitable.False;

        return _baseService.SetLogFileName(request.Value);
    }

    #region Internal

    readonly ILogger _logger;
    readonly Tools.TelemetryService<Proto.Data, Proto.Event>? _baseService;

    readonly Proto.Data _data = new()
    {
        Time = 0,
    };

    private bool StartListener()
    {
        try
        {

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError("Cannot start the service ({ex})", ex.Message);
        }

        return false;
    }

    #endregion
}
