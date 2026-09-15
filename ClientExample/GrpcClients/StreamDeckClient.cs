using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Options;

namespace ClientExample;

public class StreamDeckClient : Client
{
    public event EventHandler<bool>? ConnectionChanged;
    public event EventHandler<StreamDeck.KeyState>? KeyStateChanged;

    public bool IsConnected => _isConnected;

    public StreamDeckClient(IOptions<AppSettings> appSettings)
        : base(appSettings, (int)Common.Ports.StreamDeck)
    {
        _client = new StreamDeck.Dispatcher.DispatcherClient(_channel);
    }

    public override void Dispose()
    {
        _eventsCall?.Dispose();

        base.Dispose();
    }

    public async Task<Common.UploadResult> UploadFile(string filename)
    {
        if (!_isAvailable)
            return new Common.UploadResult() { ErrorMessage = "Service unavailable" };

        using var call = _client.UploadFile();
        return await FileService.UploadFile(call, filename, "image");
    }

    public StreamDeck.Keyboard? GetKeyboard()
    {
        if (!_isAvailable)
            return null;

        return _client.GetKeyboard(new Empty());
    }

    public void SetBrightness(int value)
    {
        if (!_isAvailable)
            return;

        _client.SetBrightness(new Common.Int() { Value = value });
    }

    public bool SetKey(StreamDeck.Key key)
    {
        if (!_isAvailable)
            return false;

        return _client.SetKey(key).Value;
    }

    #region Internal

    readonly StreamDeck.Dispatcher.DispatcherClient _client;

    bool _isConnected = false;

    AsyncServerStreamingCall<StreamDeck.Event>? _eventsCall;

    protected override void Initialize()
    {
        _isAvailable = _client.IsAvailable(new Empty()).Value;
        if (_isAvailable)
        {
            _isConnected = _client.IsConnected(new Empty()).Value;
            _ = ReadEvents();
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
                    case StreamDeck.Event.ValueOneofCase.IsConnected:
                        _isConnected = evt.IsConnected;
                        ConnectionChanged?.Invoke(this, _isConnected);
                        break;
                    case StreamDeck.Event.ValueOneofCase.Key:
                        KeyStateChanged?.Invoke(this, evt.Key);
                        break;
                    default:
                        System.Diagnostics.Debug.WriteLine($"StreamDeck event '{evt.ValueCase}' is not supported");
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