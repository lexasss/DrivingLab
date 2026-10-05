using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Options;

namespace Dashboard.GrpcClients;

public sealed class PointingClient 
    : Client<Pointing.Dispatcher.DispatcherClient, Pointing.Event>
{
    public event EventHandler<bool>? ConnectionChanged;
    public event EventHandler<Pointing.Data>? DataUpdated;

    public bool IsConnected => _isConnected;
    public bool IsStreamingData => _isStreamingData;
    public bool IsLogging => _isLogging;

    public PointingClient(IOptions<AppSettings> appSettings)
        : base(appSettings, (int)Common.Ports.Pointing)
    {
        _client = new Pointing.Dispatcher.DispatcherClient(_channel);
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

    public Pointing.Device[] GetDevices(Pointing.DeviceType type)
    {
        if (!_isAvailable)
            return [];

        var devices = _client.GetDevices(new Pointing.DeviceRequest()
        {
            Type = type
        });

        return devices.Items.ToArray();
    }

    public bool SetPointingDevice(Pointing.Device? device)
    {
        if (!_isAvailable)
            return false;

        if (device?.Description != _currentDevice?.Description)
        {
            _isConnected = device != null && _client.SetPointingDevice(device).Value;
        }
        else
        {
            _isConnected = true;
        }

        if (_isConnected && _dataCall == null)
        {
            _ = ReadData();
            _ = ReadEvents();
        }

        ConnectionChanged?.Invoke(this, _isConnected);

        return _isConnected;
    }

    public Pointing.Device? GetCurrentDevice()
    {
        if (!_isAvailable)
            return null;

        _currentDevice = _client.GetCurrentDevice(new Empty());
        return _currentDevice;
    }

    public void SetLoggingEnabled(bool enabled)
    {
        if (!_isAvailable)
            return;

        if (_isLogging != enabled)
        {
            _isLogging = _client.SetLogFileName(new Common.String()
            {
                Value = enabled
                    ? "pointing.tsv"
                    : string.Empty
            }).Value;
        }
    }

    #region Internal

    Pointing.Device? _currentDevice = null;

    AsyncServerStreamingCall<Pointing.Data>? _dataCall;

    protected override void Initialize()
    {
        _isAvailable = _client.IsAvailable(new Empty()).Value;
        if (_isAvailable)
        {
            _isStreamingData = _client.IsStreamingData(new Empty()).Value;
            _isLogging = _client.IsLogging(new Empty()).Value;
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
                DataUpdated?.Invoke(this, data);
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

    protected override IAsyncStreamReader<Pointing.Event> GetEventStream()
    {
        _eventsCall = _client.ReadEvents(new Empty());
        return _eventsCall.ResponseStream;
    }

    protected override bool HandleEvent(Pointing.Event evt)
    {
        switch (evt.ValueCase)
        {
            case Pointing.Event.ValueOneofCase.IsConnected:
                _isConnected = evt.IsConnected;
                ConnectionChanged?.Invoke(this, _isConnected);
                break;
            default:
                return false;
        }

        return true;
    }

    #endregion
}