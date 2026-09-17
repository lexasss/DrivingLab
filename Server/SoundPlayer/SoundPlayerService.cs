using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using Proto = global::SoundPlayer;

namespace Server.SoundPlayer;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")]
public class SoundPlayerService :
    Proto.Dispatcher.DispatcherBase,
    IFileService
{
    public string StorageFolder { get; } = "sounds";
    public bool IsAvailable() => true;

    public SoundPlayerService(ILoggerFactory loggerFactory) : base()
    {
        _logger = loggerFactory.CreateLogger("SNDP");

        foreach (var device in GetSoundDevices().Result)
        {
            _logger.LogInformation("Found sound device {name}", device.Name);
        }

        Tools.FileHelper.ListFiles(
            StorageFolder,
            _supportedAudioFormats,
            _logger
        );

        _baseService = new(_logger);
    }

    public void Dispose()
    {
        _tonePlayer?.Dispose();
        _audioFile?.Dispose();
        _soundPlayer?.Dispose();
        _baseService?.Dispose();

        GC.SuppressFinalize(this);
    }

    public override Task<Common.Bool> IsAvailable(
        Empty request,
        ServerCallContext context)
    {
        return Common.Bool.True;
    }

    public override async Task<Proto.Devices> GetDevices(
        Empty request,
        ServerCallContext context)
    {
        var result = new Proto.Devices();
        var devices = await GetSoundDevices();
        foreach (var device in devices)
        {
            result.Items.Add(new Proto.Device {
                Id = device.Id,
                Name = device.Name
            });
        }
        return result;
    }

    public override async Task ReadEvents(
        Empty request,
        IServerStreamWriter<Proto.Event> responseStream,
        ServerCallContext context)
    {
        if (_baseService == null)
            return;

        await _baseService.ReadEvents(request, responseStream, context);
    }

    public override Task<Common.Bool> Play(
        Proto.SoundDescription request,
        ServerCallContext context)
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
        else if (request.SoundCase == Proto.SoundDescription.SoundOneofCase.FileName)
        {
            _audioFile?.Dispose();
            _audioFile = PlayFile(_soundPlayer, request.FileName);
            result = _audioFile != null;
        }
        else
        {
            _logger.LogWarning("Unsupported sound type");
        }

        return Common.Bool.From(result);
    }

    public override Task<Empty> Stop(
        Empty request,
        ServerCallContext context)
    {
        _tonePlayer?.Stop();
        _tonePlayer?.Dispose();
        _tonePlayer = null;

        _audioFile?.Dispose();
        _audioFile = null;

        _soundPlayer?.Stop();
        _soundPlayer?.Dispose();
        _soundPlayer = null;

        _logger.LogInformation("Stopping playback");

        return Common.Constants.Empty;
    }

    public override async Task<Common.UploadResult> UploadFile(
        IAsyncStreamReader<Common.UploadRequest> requestStream,
        ServerCallContext context)
    {
        return await Tools.FileHelper.UploadFile(
            requestStream,
            context,
            StorageFolder,
            _logger);
    }

    #region Internal

    class SoundDevice(string id, string name)
    {
        public string Id => id;
        public string Name => name;
        public override string ToString() => name;
    }

    static readonly string[] _supportedAudioFormats = [".wav"];

    readonly ILogger _logger;
    readonly Tools.Service<Proto.Event> _baseService;

    WasapiPlayer? _soundPlayer;
    TonePlayer? _tonePlayer;
    AudioFileReader? _audioFile;

    private static async Task<SoundDevice[]> GetSoundDevices()
    {
        var devices = await Task.Run(() => {
            var enumerator = new MMDeviceEnumerator();
            return enumerator.EnumerateAudioEndPoints(
                DataFlow.Render,
                DeviceState.Active);
        });

        return devices
            .Select(device => new SoundDevice(device.ID, device.FriendlyName))
            .ToArray();
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
            .WithDevice(device)
            .WithEventSync()
            .WithLatency(50)
            .WithLowLatency()
            .WithCategory(AudioStreamCategory.Media)
            .WithRawMode()
            .Build();

        soundPlayer.PlaybackStopped += (sender, e) =>
        {
            _logger.LogInformation("Playback finished");
            _baseService.Publish(new Proto.Event {
                IsPlaybackFinished = true
            });
        };

        return soundPlayer;
    }

    private TonePlayer PlayTone(
        WasapiPlayer soundPlayer,
        Proto.ToneDescription tone)
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

                _logger.LogInformation("Tone finished");
                _baseService.Publish(new Proto.Event {
                    IsPlaybackFinished = true
                });
            });
        }

        _logger.LogInformation("Playing {tone}", tone.ToneType);
        return tonePlayer;
    }

    private AudioFileReader? PlayFile(
        WasapiPlayer soundPlayer,
        string filename)
    {
        string? filePath = Tools.FileHelper.FileNameToPath(
            filename,
            StorageFolder,
            _logger);

        if (string.IsNullOrEmpty(filePath))
            return null;

        AudioFileReader? audioFile = null;

        try
        {
            audioFile = new AudioFileReader(filePath);

            soundPlayer.Init(audioFile);
            soundPlayer.Play();

            _logger.LogInformation("Playing {filename}", filename);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error playing {filename}: {reason}", filename, ex.Message);
        }

        return audioFile;
    }

    #endregion
}
