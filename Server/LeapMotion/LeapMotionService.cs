using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Proto = global::LeapMotion;

namespace Server.LeapMotion;

internal class LeapMotionService : Proto.Dispatcher.DispatcherBase, ITelemetryService
{
    public bool IsAvailable() => _leap != null;

    public LeapMotionService(ILogger<LeapMotionService> logger) : base()
    {
        _logger = logger;

        try
        {
            _leap = new(_logger);
            _isConnected = _leap.IsConnected;

            _leap.ConnectionChanged += (s, e) =>
            {
                _isConnected = e;
                _events.Enqueue(new Proto.Event() { Name = Proto.Events.IS_CONNECTED, Value = _isConnected });
            };
            _leap.HandVisibilityChanged += (s, e) =>
            {
                _isHandVisible = e;
                _events.Enqueue(new Proto.Event() { Name = Proto.Events.IS_HAND_VISIBLE, Value = _isHandVisible });
            };
            _leap.HandProximityChanged += (s, e) =>
            {
                _isHandClose = e;
                _events.Enqueue(new Proto.Event() { Name = Proto.Events.IS_HAND_CLOSE, Value = _isHandClose });
            };
            _leap.HandLocationChanged += (s, e) =>
            {
                _lastSample = e;
                _hasNewData = true;
            };

            _leap.Run();

            _isActive = true;

            _logger.LogInformation("[LEAP] Service is running");
        }
        catch (Exception)
        {
            _logger.LogCritical("[LEAP] Cannot start the service");
        }
    }

    public void Dispose()
    {
        _isActive = false;
        _leap?.Dispose();
        _fileLogger.Dispose();
        _logger.LogInformation("[LEAP] Service was disposed");
    }

    public override Task<Common.Bool> IsAvailable (Empty request, ServerCallContext context)
    {
        return Task.FromResult(new Common.Bool { Value = IsAvailable() });
    }

    public override Task<Common.Bool> IsConnected(Empty request, ServerCallContext context)
    {
        return Task.FromResult(new Common.Bool { Value = _isConnected });
    }

    public override Task<Empty> Configure(Proto.Configuration request, ServerCallContext context)
    {
        _logger.LogInformation("[LEAP] [req] Configuration");

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

        return Task.FromResult(new Empty());
    }

    public override Task<Empty> Start(Empty request, ServerCallContext context)
    {
        _logger.LogInformation("[LEAP] [req] Start");
        _isSending = true;
        return Task.FromResult(new Empty());
    }

    public override Task<Empty> Stop(Empty request, ServerCallContext context)
    {
        _logger.LogInformation("[LEAP] [req] Stop");
        _isSending = false;
        return Task.FromResult(new Empty());
    }

    public override Task<Common.Bool> SetLogFilename(Common.String request, ServerCallContext context)
    {
        _logger.LogInformation("[LEAP] [req] logfile {filename}", request.Value);
        var result = _fileLogger.SetFilename(request.Value);
        return Task.FromResult(new Common.Bool() { Value = result });
    }

    public override async Task ReadData(Empty request, IServerStreamWriter<Proto.Sample> responseStream, ServerCallContext context)
    {
        _logger.LogInformation("[LEAP] [req] Read data: start");

        while (!context.CancellationToken.IsCancellationRequested)
        {
            await Task.Delay(SAMPLING_INTERVAL);

            if (_isSending && _hasNewData)
            {
                await responseStream.WriteAsync(_lastSample);
                _fileLogger.Add(_lastSample.ToStringArray());
                _hasNewData = false;
            }
        }

        _logger.LogInformation("[LEAP] [---] Read data: stop");
    }

    public override async Task ReadEvents(Empty request, IServerStreamWriter<Proto.Event> responseStream, ServerCallContext context)
    {
        while (_isActive && !context.CancellationToken.IsCancellationRequested)
        {
            await Task.Delay(5);

            if (_events.Count > 0)
            {
                var evt = _events.Dequeue();
                await responseStream.WriteAsync(evt);
            }
        }
    }

    #region Internal

    record class Event(string Name, bool Value);

    const int SAMPLING_INTERVAL = 33;

    readonly ILogger<LeapMotionService> _logger;
    readonly LeapM? _leap;
    readonly Queue<Proto.Event> _events = [];
    readonly Tools.FileLogger _fileLogger = new();

    bool _isActive = false;
    bool _isSending = false;
    bool _isConnected = false;

    Proto.Sample _lastSample = new() { Palm = Common.Vector.ZEROS };

    bool _hasNewData = false;
    bool _isHandClose = false;
    bool _isHandVisible = false;

    #endregion
}
