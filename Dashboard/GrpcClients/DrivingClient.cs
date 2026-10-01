using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Options;

namespace Dashboard;

public class DrivingClient : Client
{
    public event EventHandler<bool>? BaseConnectionChanged;
    public event EventHandler<bool>? WheelConnectionChanged;
    public event EventHandler<bool>? PedalsConnectionChanged;
    public event EventHandler<bool>? ActivePedalsHubConnectionChanged;
    public event EventHandler<bool>? ActiveBrakeConnectionChanged;
    public event EventHandler<bool>? ActiveThrottleConnectionChanged;
    public event EventHandler? EffectFinished;
    public event EventHandler<Driving.PeriodicEffectParameters>? PeriodicEffectParametersRetrieved;
    public event EventHandler<Driving.Data>? DataUpdated;

    public bool IsBaseConnected => _connectionStatus.IsBaseConnected;
    public bool IsWheelConnected => _connectionStatus.IsWheelConnected;
    public bool ArePedalsConnected => _connectionStatus.ArePedalsConnected;
    public bool IsActivePedalsHubConnected => _connectionStatus.IsActivePedalsHubConnected;
    public bool IsActiveBrakePedalConnected => _connectionStatus.IsActiveBrakePedalConnected;
    public bool IsActiveThrottlePedalConnected => _connectionStatus.IsActiveThrottlePedalConnected;
    public bool IsStreamingData => _isStreamingData;
    public bool IsLogging => _isLogging;

    public DrivingClient(IOptions<AppSettings> appSettings)
        : base(appSettings, (int)Common.Ports.Driving)
    {
        _client = new Driving.Dispatcher.DispatcherClient(_channel);
    }

    public override void Dispose()
    {
        _eventsCall?.Dispose();
        _dataCall?.Dispose();

        base.Dispose();
    }

    public void SetPeriodicEffectParameters(Driving.PeriodicEffectParameters parameters)
    {
        if (!_isAvailable)
            return;

        _periodicEffectParams = parameters;
        _client.SetPeriodicEffectParameters(parameters);
    }

    public bool PlayPedalEffect(Driving.PedalEffect pedalEffect) 
    {
        if (!_isAvailable)
            return false;

        return _client.PlayPedalEffect(pedalEffect).Value;
    }

    public void StopPedalEffect(Driving.SimucubeDevice pedal)
    {
        if (!_isAvailable)
            return;

        _client.StopPedalEffect(new Driving.StopEffect() { 
            Pedal = pedal
        });
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
            _isLogging = _client.SetLogFileName(new Common.String(
                enabled
                    ? "driving.tsv"
                    : string.Empty)
            ).Value;
        }
    }

    #region Internal

    readonly Driving.Dispatcher.DispatcherClient _client;
    
    Driving.ConnectionStatus _connectionStatus = new()
    {
        IsBaseConnected = false,
        IsWheelConnected = false,
        ArePedalsConnected = false,
        IsActivePedalsHubConnected = false,
        IsActiveBrakePedalConnected = false,
        IsActiveThrottlePedalConnected = false
    };

    bool _isStreamingData = false;
    bool _isLogging = false;

    Driving.PeriodicEffectParameters _periodicEffectParams = new();

    AsyncServerStreamingCall<Driving.Data>? _dataCall;
    AsyncServerStreamingCall<Driving.Event>? _eventsCall;

    protected override void Initialize()
    {
        _isAvailable = _client.IsAvailable(new Empty()).Value;
        if (_isAvailable)
        {
            _connectionStatus = _client.GetConnectionStatus(new Empty());
            _isStreamingData = _client.IsStreamingData(new Empty()).Value;
            _isLogging = _client.IsLogging(new Empty()).Value;

            _periodicEffectParams = _client.GetPeriodicEffectParameters(new Empty());
            PeriodicEffectParametersRetrieved?.Invoke(this, _periodicEffectParams);

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
                    case Driving.Event.ValueOneofCase.ConnectionStatus:
                        var status = evt.ConnectionStatus;
                        if (status.IsBaseConnected != _connectionStatus.IsBaseConnected)
                        {
                            _connectionStatus.IsBaseConnected = status.IsBaseConnected;
                            BaseConnectionChanged?.Invoke(this, _connectionStatus.IsBaseConnected);
                        }
                        if (status.IsWheelConnected != _connectionStatus.IsWheelConnected)
                        {
                            _connectionStatus.IsWheelConnected = status.IsWheelConnected;
                            WheelConnectionChanged?.Invoke(this, _connectionStatus.IsWheelConnected);
                        }
                        if (status.ArePedalsConnected != _connectionStatus.ArePedalsConnected)
                        {
                            _connectionStatus.ArePedalsConnected = status.ArePedalsConnected;
                            PedalsConnectionChanged?.Invoke(this, _connectionStatus.ArePedalsConnected);
                        }
                        if (status.IsActivePedalsHubConnected != _connectionStatus.IsActivePedalsHubConnected)
                        {
                            _connectionStatus.IsActivePedalsHubConnected = status.IsActivePedalsHubConnected;
                            ActivePedalsHubConnectionChanged?.Invoke(this, _connectionStatus.IsActivePedalsHubConnected);
                        }
                        if (status.IsActiveBrakePedalConnected != _connectionStatus.IsActivePedalsHubConnected)
                        {
                            _connectionStatus.IsActivePedalsHubConnected = status.IsActivePedalsHubConnected;
                            ActiveBrakeConnectionChanged?.Invoke(this, _connectionStatus.IsActivePedalsHubConnected);
                        }
                        if (status.IsActiveThrottlePedalConnected != _connectionStatus.IsActiveThrottlePedalConnected)
                        {
                            _connectionStatus.IsActiveThrottlePedalConnected = status.IsActiveThrottlePedalConnected;
                            ActiveThrottleConnectionChanged?.Invoke(this, _connectionStatus.IsActiveThrottlePedalConnected);
                        }
                        break;
                    case Driving.Event.ValueOneofCase.EffectFinished:
                        EffectFinished?.Invoke(this, EventArgs.Empty);
                        break;
                    default:
                        System.Diagnostics.Debug.WriteLine($"Driving event '{evt.ValueCase}' is not supported");
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