using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Options;

namespace ClientExample;

public class LeapMotionClient : IDisposable
{
    public record class Point(double X, double Y, double Z = 0);

    public event EventHandler<bool>? ConnectionChanged;
    public event EventHandler<bool>? HandVisibilityChanged;
    public event EventHandler<bool>? HandProximityChanged;
    public event EventHandler<LeapMotion.Sample>? HandLocationChanged;

    public bool IsAvailable => _isAvailable;
    public bool IsConnected => _isConnected;
    public bool IsReading => _isReading;

    public LeapMotionClient(IOptions<AppSettings> appSettings)
    {
        _channel = new Channel(appSettings.Value.ServerIp, (int)Common.Ports.LeapMotion, ChannelCredentials.Insecure);
        _client = new LeapMotion.Dispatcher.DispatcherClient(_channel);

        CheckAvailability();

        if (_isAvailable)
        {
            _isConnected = _client.IsConnected(new Empty()).Value;
            _ = ReadData();
            _ = ReadEvents();
        }
    }

    public void Dispose()
    {
        _eventsCts.Cancel();
        _eventsCall?.Dispose();

        _dataCts.Cancel();
        _dataCall?.Dispose();

        _channel.ShutdownAsync().Wait();

        GC.SuppressFinalize(this);
    }

    public bool CheckAvailability()
    {
        _isAvailable = false;

        try
        {
            _isAvailable = _client.IsAvailable(new Empty()).Value;
        }
        catch (RpcException ex)
        {
            Log(ex.Message);
        }

        return _isAvailable;
    }

    public void Start()
    {
        _isReading = true;
        _ = _client.Start(new Empty());
    }

    public void Stop()
    {
        _isReading = false;
        _ = _client.Stop(new Empty());
    }

    public void Configure(LeapMotion.ConfigType config)
    {
        _client.Configure(new LeapMotion.Configuration()
        {
            Config = config
        });
    }

    #region Internal

    readonly Channel _channel;
    readonly LeapMotion.Dispatcher.DispatcherClient _client;
    readonly CancellationTokenSource _dataCts = new();
    readonly CancellationTokenSource _eventsCts = new();

    bool _isAvailable = false;
    bool _isConnected = false;
    bool _isReading = false;

    AsyncServerStreamingCall<LeapMotion.Sample>? _dataCall;
    AsyncServerStreamingCall<LeapMotion.Event>? _eventsCall;

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
        catch (RpcException e)
        {
            Log(e.Message);
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
                if (evt.Name == Common.LeapMotion.Events.IS_CONNECTED)
                {
                    _isConnected = evt.Value;
                    ConnectionChanged?.Invoke(this, evt.Value);
                    if (!_isConnected)
                    {
                        HandVisibilityChanged?.Invoke(this, false);
                        HandProximityChanged?.Invoke(this, false);
                    }
                }
                else if (evt.Name == Common.LeapMotion.Events.IS_HAND_VISIBLE)
                {
                    HandVisibilityChanged?.Invoke(this, evt.Value);
                }
                else if (evt.Name == Common.LeapMotion.Events.IS_HAND_CLOSE)
                {
                    HandProximityChanged?.Invoke(this, evt.Value);
                }
                else
                {
                    // unhandled event!
                }
            }
        }
        catch (RpcException e)
        {
            Log(e.Message);
        }
        finally
        {
            _eventsCall = null;
        }
    }

    private static void Log(string msg)
    {
        System.Diagnostics.Debug.WriteLine(msg);
    }

    #endregion
}