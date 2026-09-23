using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Options;

namespace ClientExample;

public class TobiiEyeXClient : Client
{
    public event EventHandler<bool>? ConnectionStatusChanged;
    public event EventHandler<bool>? CalibrationStageChanged;
    public event EventHandler<bool>? CalibrationStatusChanged;
    public event EventHandler<bool>? TrackingStatusChanged;
    public event EventHandler<Gaze.Sample>? Sample;

    public bool IsConnected => _isConnected;
    public bool IsCalibrating => _isCalibrating;
    public bool IsCalibrated => _isCalibrated;
    public bool IsTracking => _isTracking;
    public bool IsReading => _isReading;
    public bool IsLogging => _isLogging;

    public TobiiEyeXClient(IOptions<AppSettings> appSettings)
        : base(appSettings, (int)Common.Ports.TobiiEyeX)
    {
        _client = new Gaze.Dispatcher.DispatcherClient(_channel);
    }

    public override void Dispose()
    {
        _eventsCall?.Dispose();
        _dataCall?.Dispose();

        base.Dispose();
    }

    public void Start()
    {
        if (!_isAvailable)
            return;

        _isReading = true;
        _ = _client.Start(new Empty());
    }

    public void Stop()
    {
        if (!_isAvailable)
            return;

        _isReading = false;
        _ = _client.Stop(new Empty());
    }

    public void SetLoggingEnabled(bool enabled)
    {
        if (!_isAvailable)
            return;

        _isLogging = _client.SetLogFileName(new Common.String(
            enabled 
                ? "leap.tsv"
                : string.Empty
        )).Value;
    }

    #region Internal

    readonly Gaze.Dispatcher.DispatcherClient _client;

    bool _isConnected = false;
    bool _isCalibrating = false;
    bool _isCalibrated = false;
    bool _isTracking = false;
    bool _isReading = false;
    bool _isLogging = false;

    AsyncServerStreamingCall<Gaze.Sample>? _dataCall;
    AsyncServerStreamingCall<Gaze.Event>? _eventsCall;
        
    protected override void Initialize()
    {
        _isAvailable = _client.IsAvailable(new Empty()).Value;
        if (_isAvailable)
        {
            _isConnected = _client.IsConnected(new Empty()).Value;
            _isCalibrating = _client.IsCalibrating(new Empty()).Value;
            _isCalibrated = _client.IsCalibrated(new Empty()).Value;
            _isTracking = _client.IsTracking(new Empty()).Value;

            _ = ReadData();
            _ = ReadEvents();
        }
    }

    private async Task ReadData()
    {
        try
        {
            _dataCall = _client.ReadData(new Empty());
            var responseStream = _dataCall.ResponseStream;

            while (await responseStream.MoveNext(_dataCts.Token))
            {
                var data = responseStream.Current;
                Sample?.Invoke(this, data);
            }
        }
        catch (RpcException ex)
        {
            LogException(ex);
        }
        finally
        {
            _dataCall = null;
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
                    case Gaze.Event.ValueOneofCase.Status:
                        if (_isConnected != evt.Status.IsConnected)
                        {
                            _isConnected = evt.Status.IsConnected;
                            ConnectionStatusChanged?.Invoke(this, _isConnected);
                        }
                        if (_isCalibrating != evt.Status.IsCalibrating)
                        {
                            _isCalibrating = evt.Status.IsCalibrating;
                            CalibrationStageChanged?.Invoke(this, _isCalibrating);
                        }
                        if (_isCalibrated != evt.Status.IsCalibrated)
                        {
                            _isCalibrated = evt.Status.IsCalibrated;
                            CalibrationStatusChanged?.Invoke(this, _isCalibrated);
                        }
                        if (_isTracking != evt.Status.IsTracking)
                        {
                            _isTracking = evt.Status.IsTracking;
                            TrackingStatusChanged?.Invoke(this, _isTracking);
                        }
                        break;
                    default:
                        System.Diagnostics.Debug.WriteLine($"LeapMotion event '{evt.ValueCase}' is not supported");
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