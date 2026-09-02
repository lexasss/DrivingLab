using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClientExample;

public enum PlaybackType
{
    File,
    Tone
}

public partial class SoundPlayerViewModel : ObservableObject
{
    public bool IsAvailable => _soundPlayerClient.IsAvailable;
    public SoundPlayer.Device[] Devices { get; }
    [ObservableProperty]
    public partial SoundPlayer.Device? Device { get; set; }
    [ObservableProperty]
    public partial PlaybackType PlaybackType { get; set; }
    [ObservableProperty]
    public partial string Filename { get; set; } = string.Empty;
    [ObservableProperty]
    public partial SoundPlayer.ToneType ToneType { get; set; } = SoundPlayer.ToneType.Sine;
    [ObservableProperty]
    public partial double ToneFrequency { get; set; } = 440;
    [ObservableProperty]
    public partial int ToneDuration { get; set; } = 100;
    [ObservableProperty]
    public partial double ToneGain { get; set; } = 1;
    [ObservableProperty]
    public partial bool IsPlaying { get; set; } = false;
    [ObservableProperty]
    public partial string PlayerButtonText { get; set; } = "Play";
    [ObservableProperty]
    public partial string Data { get; set; } = string.Empty;

    public SoundPlayerViewModel(SoundPlayerClient soundPlayerClient)
    {
        _soundPlayerClient = soundPlayerClient;
        _soundPlayerClient.PlaybackFinished += SoundPlayerClient_PlaybackFinished;

        Devices = _soundPlayerClient.GetDevices().Items.ToArray();
        Device = Devices.FirstOrDefault();
    }

    #region Internal

    readonly SoundPlayerClient _soundPlayerClient;

    [RelayCommand]
    private async Task Play()
    {
        if (IsPlaying)
        {
            _soundPlayerClient.Stop();
        }
        else
        {
            _soundPlayerClient.DeviceId = Device?.Id ?? string.Empty;

            if (PlaybackType == PlaybackType.File)
            {
                IsPlaying = await _soundPlayerClient.PlayFile(Filename);
            }
            else
            {
                IsPlaying = true;
                await _soundPlayerClient.PlayTone(new SoundPlayer.ToneDescription {
                    ToneType = ToneType,
                    Frequency = ToneFrequency,
                    PulseDuration = 0,
                    Gain = ToneGain,
                    TotalDuration = ToneDuration
                });
            }

            if (IsPlaying)
            {
                PlayerButtonText = "Stop";
                Data = "playing";
            }
            else
            {
                Data = "failed to play the file";
            }
        }
    }

    private void SoundPlayerClient_PlaybackFinished(object? sender, EventArgs e)
    {
        IsPlaying = false;
        PlayerButtonText = "Play";
        Data = string.Empty;

        System.Diagnostics.Debug.WriteLine("Playback finished");
    }

    #endregion
}
