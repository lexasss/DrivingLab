using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Options;

namespace Dashboard.GrpcClients;

public sealed class TensionRClient 
    : Client<TensionR.Dispatcher.DispatcherClient, TensionR.Event>
{
    public event EventHandler<bool>? ConnectionChanged;
    public event EventHandler<bool>? CalibrationChanged;
    public event EventHandler<bool>? EnabledChanged;

    public bool IsCalibrated => _isCalibrated;
    public bool IsEnabled => _isEnabled;

    public bool IsConnected => _isConnected;
    public bool IsLogging => _isLogging;

    public TensionRClient(IOptions<AppSettings> appSettings)
        : base(appSettings, (int)Common.Ports.TensionR)
    {
        _client = new TensionR.Dispatcher.DispatcherClient(_channel);
    }

    public override void Dispose()
    {
        _eventsCall?.Dispose();

        base.Dispose();
    }

    public void Connect(string port)
    {
        if (!_isAvailable)
            return;

        _ = _client.Connect(new Common.String() {
            Value = port
        });
    }

    public void SetLoggingEnabled(bool enabled)
    {
        if (!_isAvailable)
            return;

        if (_isLogging != enabled)
        {
            _isLogging = _client.SetLogFileName(new Common.String() {
                Value = enabled
                    ? "belt.tsv"
                    : string.Empty
            }).Value;
        }
    }

    public void Start()
    {
        if (!_isAvailable)
            return;

        _ = _client.Start(new Empty());
    }

    public void Stop()
    {
        if (!_isAvailable)
            return;

        _ = _client.Stop(new Empty());
    }

    public void Calibrate()
    {
        if (!_isAvailable)
            return;

        _ = _client.Calibrate(new Empty());
    }

    public void SetTension(int value, TensionR.Side side)
    {
        if (!_isAvailable)
            return;

        _ = _client.SetTension(new TensionR.Tension() {
            Value = value,
            Side = side
        });
    }

    #region Internal

    bool _isCalibrated = false;
    bool _isEnabled = false;

    protected override void Initialize()
    {
        _isAvailable = _client.IsAvailable(new Empty()).Value;
        if (_isAvailable)
        {
            _isConnected = _client.IsConnected(new Empty()).Value;
            _isCalibrated = _client.IsCalibrated(new Empty()).Value;
            _isEnabled = _client.IsEnabled(new Empty()).Value;
            _isLogging = _client.IsLogging(new Empty()).Value;

            _ = ReadEvents();
        }
    }

    protected override IAsyncStreamReader<TensionR.Event> GetEventStream()
    {
        _eventsCall = _client.ReadEvents(new Empty());
        return _eventsCall.ResponseStream;
    }

    protected override bool HandleEvent(TensionR.Event evt)
    {
        switch (evt.ValueCase)
        {
            case TensionR.Event.ValueOneofCase.IsConnected:
                _isConnected = evt.IsConnected;
                ConnectionChanged?.Invoke(this, _isConnected);
                break;
            case TensionR.Event.ValueOneofCase.IsCalibrated:
                _isCalibrated = evt.IsCalibrated;
                CalibrationChanged?.Invoke(this, _isCalibrated);
                break;
            case TensionR.Event.ValueOneofCase.IsEnabled:
                _isEnabled = evt.IsEnabled;
                EnabledChanged?.Invoke(this, _isEnabled);
                break;
            default:
                return false;
        }

        return true;
    }

    #endregion
}