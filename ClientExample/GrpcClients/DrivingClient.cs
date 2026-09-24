using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Options;

namespace ClientExample;

public class DrivingClient : Client
{
    public event EventHandler<bool>? BaseConnectionChanged;
    public event EventHandler<bool>? WheelConnectionChanged;
    public event EventHandler<bool>? PedalsConnectionChanged;
    public event EventHandler<bool>? ActivePedalsHubConnectionChanged;
    public event EventHandler<Driving.ActivePedal>? ActivePedalsConnectionChanged;
    public event EventHandler? EffectFinished;
    public event EventHandler<Driving.PeriodicEffectParameters>? PeriodicEffectParametersRetrieved;
    public event EventHandler<Driving.Data>? DataUpdated;

    public bool IsBaseConnected => _isBaseConnected;
    public bool IsWheelConnected => _isWheelConnected;
    public bool ArePedalsConnected => _arePedalsConnected;
    public bool IsActivePedalsHubConnected => _isActivePedalsHubConnected;
    public Driving.ActivePedal ActivePedalsConnected => _activePedalsConnected;
    public bool IsReading => _isReading;
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

    public void StopPedalEffect(Driving.ActivePedal pedal)
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
                ? "driving.tsv"
                : string.Empty)
        ).Value;
    }

    #region Internal

    readonly Driving.Dispatcher.DispatcherClient _client;

    bool _isBaseConnected = false;
    bool _isWheelConnected = false;
    bool _arePedalsConnected = false;
    bool _isActivePedalsHubConnected = false;
    Driving.ActivePedal _activePedalsConnected = Driving.ActivePedal.None;
    bool _isReading = false;
    bool _isLogging = false;

    Driving.PeriodicEffectParameters _periodicEffectParams = new();

    AsyncServerStreamingCall<Driving.Data>? _dataCall;
    AsyncServerStreamingCall<Driving.Event>? _eventsCall;

    protected override void Initialize()
    {
        _isAvailable = _client.IsAvailable(new Empty()).Value;
        if (_isAvailable)
        {
            var status = _client.GetConnectionStatus(new Empty());
            _isBaseConnected = status.IsBaseConnected;
            _isWheelConnected = status.IsWheelConnected;
            _arePedalsConnected = status.ArePedalsConnected;
            _isActivePedalsHubConnected = status.IsActivePedalsHubConnected;
            _activePedalsConnected = status.ActivePedalsConnected;

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
                        if (status.IsBaseConnected != _isBaseConnected)
                        {
                            _isBaseConnected = status.IsBaseConnected;
                            BaseConnectionChanged?.Invoke(this, _isBaseConnected);
                        }
                        if (status.IsWheelConnected != _isWheelConnected)
                        {
                            _isWheelConnected = status.IsWheelConnected;
                            WheelConnectionChanged?.Invoke(this, _isWheelConnected);
                        }
                        if (status.ArePedalsConnected != _arePedalsConnected)
                        {
                            _arePedalsConnected = status.ArePedalsConnected;
                            PedalsConnectionChanged?.Invoke(this, _arePedalsConnected);
                        }
                        if (status.IsActivePedalsHubConnected != _isActivePedalsHubConnected)
                        {
                            _isActivePedalsHubConnected = status.IsActivePedalsHubConnected;
                            ActivePedalsHubConnectionChanged?.Invoke(this, _isActivePedalsHubConnected);
                        }
                        if (status.ActivePedalsConnected != _activePedalsConnected)
                        {
                            _activePedalsConnected = status.ActivePedalsConnected;
                            ActivePedalsConnectionChanged?.Invoke(this, _activePedalsConnected);
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