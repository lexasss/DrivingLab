using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Options;

namespace BeltsBrakes;

public sealed class CarlaClient 
    : Client<Carla.Dispatcher.DispatcherClient, Carla.Event>
{
    public event EventHandler<Carla.Data>? DataUpdated;

    public bool IsStreamingData => _isStreamingData;
    public bool IsLogging => _isLogging;

    public string[] Fields => _client.GetTelemetryFields(new Empty()).Items.ToArray();

    public CarlaClient(IOptions<AppSettings> appSettings)
        : base(appSettings, (int)Common.Ports.Carla)
    {
        _client = new Carla.Dispatcher.DispatcherClient(_channel);
    }

    public override void Dispose()
    {
        _eventsCall?.Dispose();
        _dataCall?.Dispose();

        base.Dispose();
    }

    public void SetStreamingFields(string[] fields)
    {
        if (!_isAvailable)
            return;

        var request = new Common.Strings();
        request.Items.AddRange(fields);
        _client.SetStreamFields(request);
    }

    public string[] GetStreamingFields()
    {
        if (!_isAvailable)
            return [];

        var response = _client.GetStreamFields(new Empty());
        return response.Items.ToArray();
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
                    ? "carla.tsv"
                    : string.Empty)
            ).Value;
        }
    }

    #region Internal

    AsyncServerStreamingCall<Carla.Data>? _dataCall;

    protected override void Initialize()
    {
        _isAvailable = _client.IsAvailable(new Empty()).Value;
        if (_isAvailable)
        {
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

    protected override IAsyncStreamReader<Carla.Event> GetEventStream()
    {
        _eventsCall = _client.ReadEvents(new Empty());
        return _eventsCall.ResponseStream;
    }

    protected override bool HandleEvent(Carla.Event evt)
    {
        switch (evt.ValueCase)
        {
            case Carla.Event.ValueOneofCase.IsConnected:
                break;
            default:
                return false;
        }

        return true;
    }

    #endregion
}