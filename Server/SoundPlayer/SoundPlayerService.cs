using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using Proto = global::SoundPlayer;

namespace Server.SoundPlayer;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")]
public class SoundPlayerService : Proto.Dispatcher.DispatcherBase, IService
{
    public bool IsAvailable() => true;

    public SoundPlayerService(ILogger<SoundPlayerService> logger) : base()
    {
        _logger = logger;

        _logger.LogInformation("[SNDP] Running");
    }

    public static async Task<SoundDevice[]> GetSoundDevices()
    {
        var devices = await Task.Run(() => {
            var enumerator = new MMDeviceEnumerator();
            return enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
        });

        return devices.Select(device => new SoundDevice(device.ID, device.FriendlyName)).ToArray();
    }

    public static MMDevice? GetDevice(string id)
    {
        var enumerator = new MMDeviceEnumerator();
        var devices = enumerator.EnumerateAudioEndPoints(
            DataFlow.Render,
            DeviceState.Active);
        return devices.FirstOrDefault(d => d.ID == id);
    }

    public override Task<Common.Bool> IsAvailable(Empty request, ServerCallContext context)
    {
        return Task.FromResult(new Common.Bool { Value = true });
    }

    public override async Task<Proto.Devices> GetDevices(Empty request, ServerCallContext context)
    {
        var result = new Proto.Devices();
        var devices = await GetSoundDevices();
        foreach (var device in devices)
        {
            result.Items.Add(new Proto.Device { Id = device.Id, Name = device.Name });
        }
        return result;
    }

    public override async Task ReadEvents(Empty request, IServerStreamWriter<Proto.Event> responseStream, ServerCallContext context)
    {
        while (_isActive && !context.CancellationToken.IsCancellationRequested)
        {
            await Task.Delay(5);

            if (_events.Count > 0)
            {
                var evt = _events.Dequeue();
                await responseStream.WriteAsync(evt);
            }
        }
    }

    public override Task<Common.Bool> Play(Proto.SoundDescription request, ServerCallContext context)
    {
        bool result = false;

        if (_soundPlayer?.DeviceId != request.DeviceId)
        {
            _soundPlayer?.Dispose();

            var device = GetDevice(request.DeviceId);
            _soundPlayer = new WasapiPlayerBuilder()
               .WithDevice(device)          // default: system default render device
               .WithEventSync()             // default: event sync (vs WithPollingSync)
               .WithLatency(50)             // default: 200ms
               .WithLowLatency()            // try IAudioClient3 shared-mode low latency
               .WithCategory(AudioStreamCategory.Media)
               .WithRawMode()               // bypass system audio enhancements
               .Build();

            _soundPlayer.PlaybackStopped += (sender, e) =>
            {
                _logger.LogInformation("[SNDP] Playback finished");
                _events.Enqueue(new Proto.Event { Name = Proto.Events.PLAYBACK_FINISHED });
            };
        }

        if (request.SoundCase == Proto.SoundDescription.SoundOneofCase.Tone)
        {
            var tone = request.Tone;

            _tonePlayer?.Dispose();
            _tonePlayer = new TonePlayer(
                _soundPlayer,
                tone.ToneType,
                tone.Frequency,
                tone.Gain,
                tone.PulseDuration
            );

            _logger.LogInformation("[SNDP] Playing {tone}", tone.ToneType);
            result = true;
        }
        else if (request.SoundCase == Proto.SoundDescription.SoundOneofCase.Filename)
        {
            var filename = request.Filename;
            if (!Path.IsPathRooted(filename))
            {
                filename = Path.Combine(AppContext.BaseDirectory, "sounds", filename);
            }
            if (File.Exists(request.Filename))
            {
                using var audioFile = new AudioFileReader(request.Filename);

                _soundPlayer.Init(audioFile);
                _soundPlayer.Play();

                _logger.LogInformation("[SNDP] Playing {filename}", request.Filename);
                result = true;
            }
            else
            {
                _logger.LogWarning("[SNDP] File not found: {filename}", filename);
            }
        }

        return Task.FromResult(new Common.Bool { Value = result });
    }

    public override Task<Empty> Stop(Empty request, ServerCallContext context)
    {
        _soundPlayer?.Stop();
        _soundPlayer?.Dispose();
        _soundPlayer = null;

        _logger.LogInformation("[SNDP] Stopping playback");
        return Task.FromResult(new Empty());
    }

    public void Dispose()
    {
        _isActive = false;
        _soundPlayer?.Dispose();
        _tonePlayer?.Dispose();

        GC.SuppressFinalize(this);
    }

    #region Internal

    readonly ILogger<SoundPlayerService> _logger;
    readonly Queue<Proto.Event> _events = [];

    bool _isActive = true;

    WasapiPlayer? _soundPlayer;
    TonePlayer? _tonePlayer;

    #endregion

}
