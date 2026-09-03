using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Options;

namespace ClientExample;

public class LeapMotionClient : Client
{
    public event EventHandler<bool>? ConnectionChanged;
    public event EventHandler<bool>? HandVisibilityChanged;
    public event EventHandler<bool>? HandProximityChanged;
    public event EventHandler<LeapMotion.Sample>? HandLocationChanged;

    public bool IsConnected => _isConnected;
    public bool IsReading => _isReading;
    public bool IsLogging => _isLogging;

    public LeapMotionClient(IOptions<AppSettings> appSettings)
        : base(appSettings, (int)Common.Ports.LeapMotion)
    {
        _client = new LeapMotion.Dispatcher.DispatcherClient(_channel);
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

    public void Configure(LeapMotion.ConfigType config)
    {
        if (!_isAvailable)
            return;

        _client.Configure(new LeapMotion.Configuration()
        {
            Config = config
        });
    }

    public void SetLoggingEnabled(bool enabled)
    {
        if (!_isAvailable)
            return;

        _isLogging = _client.SetLogFileName(new Common.String() { Value = enabled ? "leap.tsv" : string.Empty }).Value;
    }

    #region Internal

    readonly LeapMotion.Dispatcher.DispatcherClient _client;

    bool _isConnected = false;
    bool _isReading = false;
    bool _isLogging = false;

    AsyncServerStreamingCall<LeapMotion.Sample>? _dataCall;
    AsyncServerStreamingCall<LeapMotion.Event>? _eventsCall;

    protected override void Initialize()
    {
        _isAvailable = _client.IsAvailable(new Empty()).Value;
        if (_isAvailable)
        {
            _isConnected = _client.IsConnected(new Empty()).Value;
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

                HandLocationChanged?.Invoke(this, data);
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
                if (evt.Name == LeapMotion.Events.IS_CONNECTED)
                {
                    _isConnected = evt.Value;
                    ConnectionChanged?.Invoke(this, evt.Value);
                    if (!_isConnected)
                    {
                        HandVisibilityChanged?.Invoke(this, false);
                        HandProximityChanged?.Invoke(this, false);
                    }
                }
                else if (evt.Name == LeapMotion.Events.IS_HAND_VISIBLE)
                {
                    HandVisibilityChanged?.Invoke(this, evt.Value);
                }
                else if (evt.Name == LeapMotion.Events.IS_HAND_CLOSE)
                {
                    HandProximityChanged?.Invoke(this, evt.Value);
                }
                else
                {
                    // unhandled event!
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