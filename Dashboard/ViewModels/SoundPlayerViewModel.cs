using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Dashboard;

public enum PlaybackType
{
    File,
    Tone
}

public class ChannelViewModel : INotifyPropertyChanged
{
    public int ChannelIndex { get; }

    public bool IsChecked
    {
        get => field;
        set
        {
            if (field == value)
                return;

            field = value;
            PropertyChanged?.Invoke(this, 
                new PropertyChangedEventArgs(nameof(IsChecked)));
            IsCheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? IsCheckedChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    public ChannelViewModel(int channelIndex, bool isChecked)
    {
        ChannelIndex = channelIndex;
        IsChecked = isChecked;
    }
}

public partial class SoundPlayerViewModel : ObservableObject
{
    public bool IsAvailable => _client.IsAvailable;
    [ObservableProperty]
    public partial SoundPlayer.Device[] Devices { get; set; } = [];
    [ObservableProperty]
    public partial SoundPlayer.Device? Device { get; set; }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanTogglePlayback))]
    [NotifyPropertyChangedFor(nameof(CanUploadFile))]
    public partial PlaybackType PlaybackType { get; set; }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanTogglePlayback))]
    [NotifyPropertyChangedFor(nameof(CanUploadFile))]
    public partial string Filename { get; set; } = string.Empty;
    [ObservableProperty]
    public partial SoundPlayer.ToneType ToneType { get; set; } = 
        SoundPlayer.ToneType.Sine;
    [ObservableProperty]
    public partial double ToneFrequency { get; set; } = 440;
    [ObservableProperty]
    public partial int ToneDuration { get; set; } = 100;
    [ObservableProperty]
    public partial double ToneGain { get; set; } = 1;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanTogglePlayback))]
    public partial bool IsPlaying { get; set; } = false;
    public bool CanTogglePlayback => IsAvailable && 
        (IsPlaying || PlaybackType == PlaybackType.Tone || Filename.Length > 0) && 
        Channels.Any(c => c.IsChecked);
    public bool CanUploadFile => IsAvailable && Filename.Length > 0;
    [ObservableProperty]
    public partial string PlayerButtonText { get; set; } = "Play";
    [ObservableProperty]
    public partial string Data { get; set; } = string.Empty;
    
    public ObservableCollection<ChannelViewModel> Channels { get; } = [];

    public SoundPlayerViewModel(
        GrpcClients.SoundPlayerClient soundPlayerClient)
    {
        _client = soundPlayerClient;
        _client.AvailabilityChanged += (s, e) =>
        {
            Devices = _client.GetDevices().Items.ToArray();
            Device = Devices.FirstOrDefault();
            OnPropertyChanged(nameof(IsAvailable));
            OnPropertyChanged(nameof(CanTogglePlayback));
            OnPropertyChanged(nameof(CanUploadFile));
        };
        _client.PlaybackFinished += SoundPlayerClient_PlaybackFinished;
    }

    #region Internal

    readonly GrpcClients.SoundPlayerClient _client;

    [RelayCommand]
    private async Task Play()
    {
        if (IsPlaying)
        {
            _client.Stop();
        }
        else
        {
            _client.DeviceId = Device?.Id ?? string.Empty;

            if (PlaybackType == PlaybackType.File)
            {
                IsPlaying = await _client.PlayFile(Filename);
            }
            else
            {
                IsPlaying = true;
                var tone = new SoundPlayer.ToneDescription {
                    ToneType = ToneType,
                    Frequency = ToneFrequency,
                    PulseDuration = 0,
                    Gain = ToneGain,
                    TotalDuration = ToneDuration,
                };

                foreach (var channel in Channels)
                    tone.EnabledChannels.Add(channel.IsChecked);

                await _client.PlayTone(tone);
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

    [RelayCommand]
    private async Task UploadFile()
    {
        if (!System.IO.File.Exists(Filename))
        {
            return;
        }

        Data = "Uploading file ...";
        try
        {
            var result = await _client.UploadFile(Filename);
            if (result.Size > 0)
            {
                Data = "File uploaded successfully.";
            }
        }
        catch
        {
            Data = "Failed to upload the file.";
        }
    }

    partial void OnDeviceChanged(SoundPlayer.Device? value)
    {
        Channels.Clear();

        if (value != null)
        {
            for (int i = 0; i < value.ChannelCount; i++)
            {
                var channel = new ChannelViewModel(i, i < 2);
                channel.IsCheckedChanged += (s, e) =>
                {
                    OnPropertyChanged(nameof(CanTogglePlayback));
                };
                Channels.Add(channel);
            }

            OnPropertyChanged(nameof(CanTogglePlayback));
        }
    }

    private void SoundPlayerClient_PlaybackFinished(
        object? sender, EventArgs e)
    {
        IsPlaying = false;
        PlayerButtonText = "Play";
        Data = string.Empty;
    }

    #endregion
}
