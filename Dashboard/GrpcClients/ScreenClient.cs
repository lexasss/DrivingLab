using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Options;

namespace Dashboard.GrpcClients;

public sealed class ScreenClient 
    : Client<Screen.Dispatcher.DispatcherClient, Screen.Event>
{
    public event EventHandler<string>? MediaHidden;

    public ScreenClient(IOptions<AppSettings> appSettings)
        : base(appSettings, (int)Common.Ports.Screen)
    {
        _client = new Screen.Dispatcher.DispatcherClient(_channel);
    }

    public override void Dispose()
    {
        _eventsCall?.Dispose();

        base.Dispose();
    }

    public Screen.Screens GetScreens()
    {
        return _isAvailable 
            ? _client.GetScreens(new Empty())
            : new Screen.Screens();
    }

    public async Task<string?> Show(
        string filename,
        int screenId,
        Common.Point location,
        Common.Size? size,
        int? duration)
    {
        if (!_isAvailable)
            return null;

        var response = await _client.ShowAsync(new Screen.Media()
        {
            FileName = filename,
            ScreenId = screenId,
            Location = location,
            Size = size,
            Duration = duration ?? 0
        });

        return string.IsNullOrEmpty(response?.Value)
            ? null
            : response.Value;
    }

    public void Hide(string id)
    {
        if (!_isAvailable)
            return;

        _ = _client.Close(new Common.String { Value = id });
    }

    public async Task<Common.UploadResult> UploadFile(string filename)
    {
        if (!_isAvailable)
            return new Common.UploadResult() { 
                ErrorMessage = "Service unavailable"
            };

        using var call = _client.UploadFile();
        return await FileService.UploadFile(call, filename, "image");
    }

    #region Internal

    protected override void Initialize()
    {
        _isAvailable = _client.IsAvailable(new Empty()).Value;
        if (_isAvailable)
        {
            _ = ReadEvents();
        }
    }

    protected override IAsyncStreamReader<Screen.Event> GetEventStream()
    {
        _eventsCall = _client.ReadEvents(new Empty());
        return _eventsCall.ResponseStream;
    }

    protected override bool HandleEvent(Screen.Event evt)
    {
        switch (evt.ValueCase)
        {
            case Screen.Event.ValueOneofCase.HiddenMediaId:
                MediaHidden?.Invoke(this, evt.HiddenMediaId);
                break;
            default:
                return false;
        }

        return true;
    }

    #endregion
}