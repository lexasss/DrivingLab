using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Options;

namespace ClientExample;

public class TensionRClient : Client
{
    public event EventHandler<bool>? ConnectionChanged;
    public event EventHandler<bool>? CalibrationChanged;
    public event EventHandler<bool>? EnabledChanged;

    public bool IsConnected => _isConnected;
    public bool IsCalibrated => _isCalibrated;
    public bool IsEnabled => _isEnabled;
    public bool IsLogging => _isLogging;

    public TensionRClient(IOptions<AppSettings> appSettings)
        : base(appSettings, (int)Common.Ports.TensionR)
    {
        _client = new TensionR.Dispatcher.DispatcherClient(_channel);

        _isConnected = _client.IsConnected(new Empty()).Value;
        _isCalibrated = _client.IsCalibrated(new Empty()).Value;
        _isEnabled = _client.IsEnabled(new Empty()).Value;
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

        _ = _client.Connect(new Common.String() { Value = port });
    }

    public void SetLoggingEnabled(bool enabled)
    {
        if (!_isAvailable)
            return;

        _isLogging = _client.SetLogFileName(new Common.String() { Value = enabled ? "belt.tsv" : string.Empty }).Value;
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

    readonly TensionR.Dispatcher.DispatcherClient _client;

    bool _isConnected = false;
    bool _isCalibrated = false;
    bool _isEnabled = false;
    bool _isLogging = false;

    AsyncServerStreamingCall<TensionR.Event>? _eventsCall;

    protected override void Initialize()
    {
        _isAvailable = _client.IsAvailable(new Empty()).Value;
        if (_isAvailable)
        {
            _ = ReadEvents();
        }
    }

    private async Task ReadEvents()
    {
        try
        {
            _eventsCall = _client.ReadEvents(new Empty());
            var responseStream = _eventsCall.ResponseStream;

            while (await responseStream.MoveNext(_eventsCts.Token))
            {
                if (_eventsCts.IsCancellationRequested)
                    break;

                var evt = responseStream.Current;
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
                        System.Diagnostics.Debug.WriteLine($"TensionR event '{evt.ValueCase}' is not supported");
                        break;
                }
            }
        }
        catch (RpcException ex)
        {
            LogException(ex);
        }
        finally
        {
            _eventsCall = null;
        }
    }

    #endregion
}