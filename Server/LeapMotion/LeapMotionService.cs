using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Proto = global::LeapMotion;

namespace Server.LeapMotion;

internal class LeapMotionService :
    Proto.Dispatcher.DispatcherBase,
    ITelemetryService
{
    public bool IsAvailable() => _leap != null;

    public LeapMotionService(ILoggerFactory loggerFactory) : base()
    {
        _logger = loggerFactory.CreateLogger("LEAP");

        try
        {
            _leap = new(_logger);
            _isConnected = _leap.IsConnected;

            _leap.ConnectionChanged += (s, e) =>
            {
                _isConnected = e;
                _baseService?.Publish(new Proto.Event() {
                    IsConnected = _isConnected
                });
            };
            _leap.HandVisibilityChanged += (s, e) =>
            {
                _isHandVisible = e;
                _baseService?.Publish(new Proto.Event() {
                    IsHandVisible = _isHandVisible
                });
            };
            _leap.HandProximityChanged += (s, e) =>
            {
                _isHandClose = e;
                _baseService?.Publish(new Proto.Event() {
                    IsHandClose = _isHandClose
                });
            };
            _leap.HandLocationChanged += (s, e) =>
            {
                _baseService?.Publish(e);
            };

            _leap.Run();

            _baseService = new(_logger);

            _logger.LogInformation("Running");
        }
        catch (Exception)
        {
            _logger.LogError("Cannot start the service");
        }
    }

    public void Dispose()
    {
        _leap?.Dispose();
        _baseService?.Dispose();

        GC.SuppressFinalize(this);
    }

    public override Task<Common.Bool> IsAvailable(
        Empty request,
        ServerCallContext context)
    {
        return Common.Bool.From(IsAvailable());
    }

    public override Task<Common.Bool> IsConnected(
        Empty request,
        ServerCallContext context)
    {
        return Common.Bool.From(_isConnected);
    }

    public override Task<Empty> Configure(Proto.Configuration request, ServerCallContext context)
    {
        if (request.Config == Proto.ConfigType.Custom)
        {
            _leap?.SetProximityBox(
                request.ProximityBoxCorner1,
                request.ProximityBoxCorner2
            );
            _leap?.SetTransform(
                request.Translation,
                request.Scale
            );
        }
        else if (request.Config == Proto.ConfigType.Ultrahaptics)
        {
            _leap?.ConfigureForUltrahaptics();
        }
        else
        {
            _leap?.SetProximityBox();
            _leap?.SetTransform();
        }

        _logger.LogInformation("Configured as '{type}'", request.Config);

        return Common.Constants.Empty;
    }

    public override Task<Empty> Start(
        Empty request,
        ServerCallContext context)
    {
        _baseService?.Start();
        return Common.Constants.Empty;
    }

    public override Task<Empty> Stop(
        Empty request,
        ServerCallContext context)
    {
        _baseService?.Stop();
        return Common.Constants.Empty;
    }

    public override Task<Common.Bool> SetLogFileName(
        Common.String request,
        ServerCallContext context)
    {
        if (_baseService == null)
            return Common.Bool.False;

        return _baseService.SetLogFileName(request.Value);
    }

    public override async Task ReadData(
        Empty request,
        IServerStreamWriter<Proto.Sample> responseStream,
        ServerCallContext context)
    {
        if (_baseService == null)
            return;

        await _baseService.ReadData(request, responseStream, context, 1);
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

    #region Internal

    record class Event(string Name, bool Value);

    readonly ILogger _logger;
    readonly LeapM? _leap;
    readonly Tools.TelemetryService<Proto.Sample, Proto.Event>? _baseService;

    bool _isConnected = false;

    bool _isHandClose = false;
    bool _isHandVisible = false;

    #endregion
}
