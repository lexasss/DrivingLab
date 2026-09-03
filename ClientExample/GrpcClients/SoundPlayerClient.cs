using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Options;

namespace ClientExample;

public class SoundPlayerClient : Client
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
        return _isAvailable ? _client.GetDevices(new Empty()) : new SoundPlayer.Devices();
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

    #region Internal

    readonly SoundPlayer.Dispatcher.DispatcherClient _client;

    AsyncServerStreamingCall<SoundPlayer.Event>? _eventsCall;

    protected override void Initialize()
    {
        _isAvailable = _client.IsAvailable(new Empty()).Value;
        if (_isAvailable)
        {
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
                if (evt.Name == SoundPlayer.Events.PLAYBACK_FINISHED)
                {
                    PlaybackFinished?.Invoke(this, EventArgs.Empty);
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