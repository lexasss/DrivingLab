using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Options;

namespace Dashboard.GrpcClients;

public sealed class SmartEyeClient 
    : Client<SmartEye.Dispatcher.DispatcherClient, SmartEye.Event>
{
    public event EventHandler<bool>? ConnectionChanged;
    public event EventHandler<SmartEye.Intersection>? IntersectionChanged;

    public bool IsConnected => _isConnected;
    public bool IsStreamingData => _isStreamingData;
    public bool IsLogging => _isLogging;

    public SmartEyeClient(IOptions<AppSettings> appSettings)
        : base(appSettings, (int)Common.Ports.SmartEye)
    {
        _client = new SmartEye.Dispatcher.DispatcherClient(_channel);
    }

    public override void Dispose()
    {
        _eventsCall?.Dispose();

        base.Dispose();
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

    public async Task<bool> ConfigureAsync(
        string ip,
        SmartEye.IntersectionSource intersectionSource,
        bool useFilteredData)
    {
        if (!_isAvailable)
            return false;

        var result = await _client.ConfigureAsync(new SmartEye.Configuration()
        {
            Ip = ip,
            Port = SmartEye.Consts.DefaultPort,
            IntersectionSource = intersectionSource,
            UseFilteredData = useFilteredData,
            PlaneMappingMode = SmartEye.PlaneMappingMode.Closest
        });
        return result.Value;
    }

    public void SetLoggingEnabled(bool enabled)
    {
        if (!_isAvailable)
            return;

        if (_isLogging != enabled)
        {
            _isLogging = _client.SetLogFileName(new Common.String() {
                Value = enabled
                    ? "se.tsv"
                    : string.Empty
            }).Value;
        }
    }

    #region Internal

    protected override void Initialize()
    {
        _isAvailable = _client.IsAvailable(new Empty()).Value;
        if (_isAvailable)
        {
            _isConnected = _client.IsConnected(new Empty()).Value;
            _isStreamingData = _client.IsStreamingData(new Empty()).Value;
            _isLogging = _client.IsLogging(new Empty()).Value;

            _ = ReadEvents();
        }
    }

    protected override IAsyncStreamReader<SmartEye.Event> GetEventStream()
    {
        _eventsCall = _client.ReadEvents(new Empty());
        return _eventsCall.ResponseStream;
    }

    protected override bool HandleEvent(SmartEye.Event evt)
    {
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
                return false;
        }

        return true;
    }

    #endregion
}