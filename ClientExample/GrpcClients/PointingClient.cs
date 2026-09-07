using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Options;

namespace ClientExample;

public class PointingClient : Client
{
    public event EventHandler<bool>? ConnectionChanged;
    public event EventHandler<Pointing.Data>? DataUpdated;

    public bool IsConnected => _isConnected;
    public bool IsReading => _isReading;
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

        _isConnected = device != null
            ? _client.SetPointingDevice(device).Value
            : false;

        if (_isConnected && _dataCall == null)
        {
            _ = ReadData();
            _ = ReadEvents();
        }

        ConnectionChanged?.Invoke(this, _isConnected);

        return _isConnected;
    }

    public void SetLoggingEnabled(bool enabled)
    {
        if (!_isAvailable)
            return;

        _isLogging = _client.SetLogFileName(new Common.String() { Value = enabled ? "pointing.tsv" : string.Empty }).Value;
    }

    #region Internal

    readonly Pointing.Dispatcher.DispatcherClient _client;

    bool _isConnected = false;
    bool _isReading = false;
    bool _isLogging = false;

    AsyncServerStreamingCall<Pointing.Data>? _dataCall;
    AsyncServerStreamingCall<Pointing.Event>? _eventsCall;

    protected override void Initialize()
    {
        _isAvailable = _client.IsAvailable(new Empty()).Value;
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
                    case Pointing.Event.ValueOneofCase.IsConnected:
                        _isConnected = evt.IsConnected;
                        ConnectionChanged?.Invoke(this, _isConnected);
                        break;
                    default:
                        System.Diagnostics.Debug.WriteLine($"Pointing event '{evt.ValueCase}' is not supported");
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