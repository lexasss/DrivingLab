using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Options;

namespace ClientExample;

public class SmartEyeClient : IDisposable
{
    public event EventHandler<bool>? ConnectionChanged;
    public event EventHandler<SmartEye.Intersection>? IntersectionChanged;

    public bool IsAvailable => _isAvailable;
    public bool IsConnected => _isConnected;
    public bool IsLogging => _isLogging;

    public SmartEyeClient(IOptions<AppSettings> appSettings)
    {
        _channel = new Channel(appSettings.Value.ServerIp, (int)Common.Ports.SmartEye, ChannelCredentials.Insecure);
        _client = new SmartEye.Dispatcher.DispatcherClient(_channel);

        CheckAvailability();

        if (_isAvailable)
        {
            _isConnected = _client.IsConnected(new Empty()).Value;
            _ = ReadEvents();
        }
    }

    public void Dispose()
    {
        _eventsCts.Cancel();
        _eventsCall?.Dispose();

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
        _ = _client.Start(new Empty());
    }

    public void Stop()
    {
        _ = _client.Stop(new Empty());
    }

    public async Task<bool> ConfigureAsync(string ip)
    {
        var result = await _client.ConfigureAsync(new SmartEye.Configuration()
        {
            Ip = ip,
            Port = SmartEye.Consts.DefaultPort,
            IntersectionSource = SmartEye.IntersectionSource.Gaze,
            UseFilteredData = true,
            PlaneMappingMode = SmartEye.PlaneMappingMode.Closest
        });
        return result.Value;
    }

    public void SetLoggingEnabled(bool enabled)
    {
        if (enabled)
        {
            var task = _client.SetLogFilenameAsync(new Common.String() { Value = "se.tsv" });
            var awaiter = task.GetAwaiter();
            awaiter.OnCompleted(() => _isLogging = awaiter.GetResult().Value);
        }
        else
        {
            _ = _client.SetLogFilenameAsync(new Common.String() { Value = string.Empty });
            _isLogging = false;
        }
    }

    #region Internal

    readonly Channel _channel;
    readonly SmartEye.Dispatcher.DispatcherClient _client;
    readonly CancellationTokenSource _eventsCts = new();

    bool _isAvailable = false;
    bool _isConnected = false;
    bool _isLogging = false;

    AsyncServerStreamingCall<SmartEye.Event>? _eventsCall;

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
                    case SmartEye.Event.ValueOneofCase.IsConnected:
                        _isConnected = evt.IsConnected;
                        ConnectionChanged?.Invoke(this, _isConnected);
                        if (!_isConnected)
                        {
                            // 
                        }
                        break;
                    case SmartEye.Event.ValueOneofCase.Intersection:
                        IntersectionChanged?.Invoke(this, evt.Intersection);
                        break;
                    case SmartEye.Event.ValueOneofCase.Intersections:
                        // it was not configured for this event to receive
                        break;
                    default:
                        System.Diagnostics.Debug.WriteLine($"SmartEye event '{evt.ValueCase}' is not supported");
                        break;
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