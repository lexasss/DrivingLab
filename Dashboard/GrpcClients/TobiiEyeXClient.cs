using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Options;

namespace Dashboard.GrpcClients;

public sealed class TobiiEyeXClient
    : Client<Gaze.Dispatcher.DispatcherClient, Gaze.Event>
{
    public event EventHandler<bool>? ConnectionStatusChanged;
    public event EventHandler<bool>? CalibrationStageChanged;
    public event EventHandler<bool>? CalibrationStatusChanged;
    public event EventHandler<bool>? TrackingStatusChanged;
    public event EventHandler<Gaze.Sample>? Sample;

    public bool IsCalibrating => _isCalibrating;
    public bool IsCalibrated => _isCalibrated;
    public bool IsTracking => _isTracking;

    public bool IsConnected => _isConnected;
    public bool IsStreamingData => _isStreamingData;
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

        _isStreamingData = true;
        _ = _client.Start(new Empty());
    }

    public void Stop()
    {
        if (!_isAvailable)
            return;

        _isStreamingData = false;
        _ = _client.Stop(new Empty());
    }

    public void SetLoggingEnabled(bool enabled)
    {
        if (!_isAvailable)
            return;

        if (_isLogging != enabled)
        {
            _isLogging = _client.SetLogFileName(new Common.String() {
                Value = enabled
                    ? "eyex.tsv"
                    : string.Empty
            }).Value;
        }
    }

    #region Internal

    bool _isCalibrating = false;
    bool _isCalibrated = false;
    bool _isTracking = false;

    AsyncServerStreamingCall<Gaze.Sample>? _dataCall;
        
    protected override void Initialize()
    {
        _isAvailable = _client.IsAvailable(new Empty()).Value;
        if (_isAvailable)
        {
            _isConnected = _client.IsConnected(new Empty()).Value;
            _isCalibrating = _client.IsCalibrating(new Empty()).Value;
            _isCalibrated = _client.IsCalibrated(new Empty()).Value;
            _isTracking = _client.IsTracking(new Empty()).Value;

            _isStreamingData = _client.IsStreamingData(new Empty()).Value;
            _isLogging = _client.IsLogging(new Empty()).Value;

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

    protected override IAsyncStreamReader<Gaze.Event> GetEventStream()
    {
        _eventsCall = _client.ReadEvents(new Empty());
        return _eventsCall.ResponseStream;
    }

    protected override bool HandleEvent(Gaze.Event evt)
    {
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
                return false;
        }

        return true;
    }

    #endregion
}