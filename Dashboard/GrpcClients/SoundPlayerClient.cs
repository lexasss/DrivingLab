using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Options;

namespace Dashboard.GrpcClients;

public sealed class SoundPlayerClient 
    : Client<SoundPlayer.Dispatcher.DispatcherClient, SoundPlayer.Event>
{
    public string DeviceId { get; set; } = string.Empty;

    public event EventHandler? PlaybackFinished;

    public SoundPlayerClient(IOptions<AppSettings> appSettings)
        : base(appSettings, (int)Common.Ports.SoundPlayer)
    {
        _client = new SoundPlayer.Dispatcher.DispatcherClient(_channel);
    }

    public override void Dispose()
    {
        _eventsCall?.Dispose();

        base.Dispose();
    }

    public SoundPlayer.Devices GetDevices()
    {
        return _isAvailable
            ? _client.GetDevices(new Empty())
            : new SoundPlayer.Devices();
    }

    public async Task<bool> PlayFile(string filename)
    {
        if (!_isAvailable)
            return false;

        var response = await _client.PlayAsync(new SoundPlayer.SoundDescription()
        {
            DeviceId = DeviceId,
            FileName = filename,
        });
        return response.Value;
    }

    public async Task PlayTone(SoundPlayer.ToneDescription tone)
    {
        if (!_isAvailable)
            return;

        await _client.PlayAsync(new SoundPlayer.SoundDescription()
        {
            DeviceId = DeviceId,
            Tone = tone,
        });
    }

    public void Stop()
    {
        if (!_isAvailable)
            return;

        _ = _client.Stop(new Empty());
    }

    public async Task<Common.UploadResult> UploadFile(string filename)
    {
        if (!_isAvailable)
            return new Common.UploadResult() {
                ErrorMessage = "Service unavailable"
            };

        using var call = _client.UploadFile();
        return await FileService.UploadFile(call, filename, "audio");
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

    protected override IAsyncStreamReader<SoundPlayer.Event> GetEventStream()
    {
        _eventsCall = _client.ReadEvents(new Empty());
        return _eventsCall.ResponseStream;
    }

    protected override bool HandleEvent(SoundPlayer.Event evt)
    {
        switch (evt.ValueCase)
        {
            case SoundPlayer.Event.ValueOneofCase.IsPlaybackFinished:
                PlaybackFinished?.Invoke(this, EventArgs.Empty);
                break;
            default:
                return false;
        }

        return true;
    }

    #endregion
}