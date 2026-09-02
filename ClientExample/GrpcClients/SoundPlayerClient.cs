using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Options;

namespace ClientExample;

public class SoundPlayerClient : IDisposable
{
    public bool IsAvailable => _isAvailable;
    public string DeviceId { get; set; } = string.Empty;

    public event EventHandler? PlaybackFinished;

    public SoundPlayerClient(IOptions<AppSettings> appSettings)
    {
        _channel = new Channel(appSettings.Value.ServerIp, (int)Common.Ports.SoundPlayer, ChannelCredentials.Insecure);
        _client = new SoundPlayer.Dispatcher.DispatcherClient(_channel);

        _isAvailable = _client.IsAvailable(new Empty()).Value;

        if (_isAvailable)
        {
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

    public SoundPlayer.Devices GetDevices() =>
        _client.GetDevices(new Empty());

    public async Task<bool> PlayFile(string filename)
    {
        var response = await _client.PlayAsync(new SoundPlayer.SoundDescription()
        {
            DeviceId = DeviceId,
            Filename = filename,
        });
        return response.Value;
    }

    public async Task PlayTone(SoundPlayer.ToneDescription tone)
    {
        await _client.PlayAsync(new SoundPlayer.SoundDescription()
        {
            DeviceId = DeviceId,
            Tone = tone,
        });
    }

    public void Stop()
    {
        _ = _client.Stop(new Empty());
    }

    #region Internal

    readonly Channel _channel;
    readonly SoundPlayer.Dispatcher.DispatcherClient _client;
    readonly CancellationTokenSource _eventsCts = new();

    bool _isAvailable = false;

    AsyncServerStreamingCall<SoundPlayer.Event>? _eventsCall;

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

    private static void LogException(Exception ex)
    {
        System.Diagnostics.Debug.WriteLine(ex.Message);
    }

    #endregion
}