using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace Server.LeapMotion;

internal class LeapMotionService : global::LeapMotion.Dispatcher.DispatcherBase, ITelemetryService
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
                _events.Enqueue(new global::LeapMotion.Event() { Name = Common.LeapMotionEvents.IS_CONNECTED, Value = _isConnected });
            };
            _leap.HandVisibilityChanged += (s, e) =>
            {
                _isHandVisible = e;
                _events.Enqueue(new global::LeapMotion.Event() { Name = Common.LeapMotionEvents.IS_HAND_VISIBLE, Value = _isHandVisible });
            };
            _leap.HandProximityChanged += (s, e) =>
            {
                _isHandClose = e;
                _events.Enqueue(new global::LeapMotion.Event() { Name = Common.LeapMotionEvents.IS_HAND_CLOSE, Value = _isHandClose });
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
        _logger.LogInformation("[LEAP] Service was disposed");
        _leap?.Dispose();
    }

    public override Task<Common.BoolReply> IsAvailable (Empty request, ServerCallContext context)
    {
        return Task.FromResult(new Common.BoolReply { Success = IsAvailable() });
    }

    public override Task<Common.BoolReply> IsConnected(Empty request, ServerCallContext context)
    {
        return Task.FromResult(new Common.BoolReply { Success = _isConnected });
    }

    public override Task<Empty> Start(Empty request, ServerCallContext context)
    {
        _isSending = true;
        _logger.LogInformation("[LEAP] Streaming started");
        return Task.FromResult(new Empty());
    }

    public override Task<Empty> Stop(Empty request, ServerCallContext context)
    {
        _isSending = false;
        _logger.LogInformation("[LEAP] Streaming finished");
        return Task.FromResult(new Empty());
    }

    public override async Task ReadData(Empty request, IServerStreamWriter<global::LeapMotion.Sample> responseStream, ServerCallContext context)
    {
        _logger.LogInformation("[LEAP] Reading cycle started");

        while (!context.CancellationToken.IsCancellationRequested)
        {
            await Task.Delay(SAMPLING_INTERVAL);

            if (_isSending && _hasNewData)
            {
                await responseStream.WriteAsync(new global::LeapMotion.Sample
                {
                    X = _lastSample.x,
                    Y = _lastSample.y,
                    Z = _lastSample.z,
                });
                _hasNewData = false;
            }
        }

        _logger.LogInformation("[LEAP] Reading cycle stopped");
    }

    public override async Task ReadEvents(Empty request, IServerStreamWriter<global::LeapMotion.Event> responseStream, ServerCallContext context)
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

    // Internal

    record class Event(string Name, bool Value);

    const int SAMPLING_INTERVAL = 33;

    readonly ILogger<LeapMotionService> _logger;
    readonly LeapM? _leap;
    readonly Queue<global::LeapMotion.Event> _events = [];

    bool _isActive = false;
    bool _isSending = false;
    bool _isConnected = false;

    Leap.Vector _lastSample = Leap.Vector.Zero;

    bool _hasNewData = false;
    bool _isHandClose = false;
    bool _isHandVisible = false;
}
