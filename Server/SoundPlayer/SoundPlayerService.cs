using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using System.IO;
using Proto = global::SoundPlayer;

namespace Server.SoundPlayer;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")]
public class SoundPlayerService : Proto.Dispatcher.DispatcherBase, IService
{
    public bool IsAvailable() => true;

    public SoundPlayerService(ILogger<SoundPlayerService> logger) : base()
    {
        _logger = logger;

        foreach (var device in GetSoundDevices().Result)
        {
            _logger.LogInformation("[SNDP] Found sound device {name}", device.Name);
        }

        try
        {
            foreach (var file in Directory.EnumerateFiles(SOUND_FOLDER, "*.wav"))
            {
                _logger.LogInformation("[SNDP] Found sound file {file}", Path.GetFileNameWithoutExtension(file));
            }
        }
        catch
        {
            _logger.LogWarning("[SNDP] Sound folder does not exist");
        }

        _logger.LogInformation("[SNDP] Running");
    }

    public void Dispose()
    {
        _isActive = false;

        _tonePlayer?.Dispose();
        _audioFile?.Dispose();
        _soundPlayer?.Dispose();

        _logger.LogInformation("[SNDP] Disposed");

        GC.SuppressFinalize(this);
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

        _soundPlayer?.Dispose();
        _soundPlayer = CreatePlayer(request.DeviceId);

        if (request.SoundCase == Proto.SoundDescription.SoundOneofCase.Tone)
        {
            _tonePlayer?.Dispose();
            _tonePlayer = PlayTone(_soundPlayer, request.Tone);
            result = true;
        }
        else if (request.SoundCase == Proto.SoundDescription.SoundOneofCase.Filename)
        {
            _audioFile?.Dispose();
            _audioFile = PlayFile(_soundPlayer, request.Filename);
            result = _audioFile != null;
        }
        else
        {
            _logger.LogWarning("[SNDP] Unsupported sound type");
        }

        return Task.FromResult(new Common.Bool { Value = result });
    }

    public override Task<Empty> Stop(Empty request, ServerCallContext context)
    {
        _tonePlayer?.Stop();
        _tonePlayer?.Dispose();
        _tonePlayer = null;
        _audioFile?.Dispose();
        _audioFile = null;

        _soundPlayer?.Stop();
        _soundPlayer?.Dispose();
        _soundPlayer = null;

        _logger.LogInformation("[SNDP] Stopping playback");
        return Task.FromResult(new Empty());
    }

    #region Internal

    class SoundDevice(string id, string name)
    {
        public string Id => id;
        public string Name => name;
        public override string ToString() => name;
    }

    const string SOUND_FOLDER = "sounds";

    readonly ILogger<SoundPlayerService> _logger;
    readonly Queue<Proto.Event> _events = [];

    bool _isActive = true;

    WasapiPlayer? _soundPlayer;
    TonePlayer? _tonePlayer;
    AudioFileReader? _audioFile;

    private static async Task<SoundDevice[]> GetSoundDevices()
    {
        var devices = await Task.Run(() => {
            var enumerator = new MMDeviceEnumerator();
            return enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
        });

        return devices.Select(device => new SoundDevice(device.ID, device.FriendlyName)).ToArray();
    }

    private static MMDevice? GetDevice(string id)
    {
        var enumerator = new MMDeviceEnumerator();
        var devices = enumerator.EnumerateAudioEndPoints(
            DataFlow.Render,
            DeviceState.Active);
        return devices.FirstOrDefault(d => d.ID == id);
    }

    private WasapiPlayer CreatePlayer(string deviceId)
    {
        var device = GetDevice(deviceId);
        var soundPlayer = new WasapiPlayerBuilder()
            .WithDevice(device)          // default: system default render device
            .WithEventSync()             // default: event sync (vs WithPollingSync)
            .WithLatency(50)             // default: 200ms
            .WithLowLatency()            // try IAudioClient3 shared-mode low latency
            .WithCategory(AudioStreamCategory.Media)
            .WithRawMode()               // bypass system audio enhancements
            .Build();

        soundPlayer.PlaybackStopped += (sender, e) =>
        {
            _logger.LogInformation("[SNDP] Playback finished");
            _events.Enqueue(new Proto.Event { Name = Proto.Events.PLAYBACK_FINISHED });
        };

        return soundPlayer;
    }

    private TonePlayer PlayTone(WasapiPlayer soundPlayer, Proto.ToneDescription tone)
    {
        var tonePlayer = new TonePlayer(
            soundPlayer,
            tone.ToneType,
            tone.Frequency,
            tone.Gain,
            tone.PulseDuration
        );

        tonePlayer.Start();
        
        if (tone.TotalDuration > 0)
        {
            Task.Run(async () =>
            {
                await Task.Delay(tone.TotalDuration);

                tonePlayer.Stop();

                _logger.LogInformation("[SNDP] Tone finished");
                _events.Enqueue(new Proto.Event { Name = Proto.Events.PLAYBACK_FINISHED });
            });
        }

        _logger.LogInformation("[SNDP] Playing {tone}", tone.ToneType);
        return tonePlayer;
    }

    private AudioFileReader? PlayFile(WasapiPlayer soundPlayer, string filename)
    {
        AudioFileReader? audioFile = null;

        var filePath = filename;
        if (!Path.IsPathRooted(filePath))
        {
            filePath = Path.Combine(AppContext.BaseDirectory, SOUND_FOLDER, filename);
        }

        if (File.Exists(filePath))
        {
            try
            {
                audioFile = new AudioFileReader(filePath);

                soundPlayer.Init(audioFile);
                soundPlayer.Play();

                _logger.LogInformation("[SNDP] Playing {filename}", filename);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SNDP] Error playing {filename}: {reason}", filename, ex.Message);
            }
        }
        else
        {
            _logger.LogWarning("[SNDP] File not found: {filename}", filePath);
        }

        return audioFile;
    }

    #endregion
}
