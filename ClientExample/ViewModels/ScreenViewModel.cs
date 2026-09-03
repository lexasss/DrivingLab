using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClientExample;

public partial class ScreenViewModel : ObservableObject
{
    public bool IsAvailable => _screenClient.IsAvailable;
    [ObservableProperty]
    public partial Screen.Screen[] Screens { get; set; } = [];
    [ObservableProperty]
    public partial Screen.Screen? Screen { get; set; }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanToggleMedia))]
    public partial string Filename { get; set; } = string.Empty;
    [ObservableProperty]
    public partial int X { get; set; } = 0;
    [ObservableProperty]
    public partial int Y { get; set; } = 0;
    [ObservableProperty]
    public partial int Width { get; set; } = 0;
    [ObservableProperty]
    public partial int Height { get; set; } = 0;
    [ObservableProperty]
    public partial int Duration { get; set; } = 0;
    public bool CanToggleMedia => IsAvailable && (_mediaId != null || Filename.Length > 0);
    [ObservableProperty]
    public partial string ShowButtonText { get; set; } = "Show";
    [ObservableProperty]
    public partial string Data { get; set; } = string.Empty;

    public ScreenViewModel(ScreenClient screenClient)
    {
        _screenClient = screenClient;
        _screenClient.AvailabilityChanged += (s, e) =>
        {
            Screens = _screenClient.GetScreens().Items.ToArray();
            Screen = Screens.FirstOrDefault();
            OnPropertyChanged(nameof(IsAvailable));
            OnPropertyChanged(nameof(CanToggleMedia));
        };
        _screenClient.MediaHidden += ScreenClient_MediaHidden;
    }

    #region Internal

    readonly ScreenClient _screenClient;

    string? _mediaId = null;

    [RelayCommand]
    private async Task Show()
    {
        string message = string.Empty;

        if (_mediaId != null)
        {
            _screenClient.Hide(_mediaId);
            _mediaId = null;
        }
        else
        {
            _mediaId = await _screenClient.Show(
                Filename,
                Screen?.Id ?? 0,
                new Common.Point { X = X, Y = Y },
                new Common.Size { Width = Width, Height = Height },
                Duration
            );

            message = _mediaId == null ? "media is not available" : "media is visible ...";
        }

        UpdateUI(message);
    }

    private void ScreenClient_MediaHidden(object? sender, string id)
    {
        _mediaId = null;

        UpdateUI(string.Empty);

        System.Diagnostics.Debug.WriteLine($"Media {id} is hidden");
    }

    private void UpdateUI(string message)
    {
        ShowButtonText = _mediaId != null ? "Hide" : "Show";
        Data = message;
        OnPropertyChanged(nameof(CanToggleMedia));
    }

    #endregion
}
