using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClientExample;

public partial class SmartEyeViewModel : ObservableObject
{
    public bool IsAvailable => _smartEyeClient.IsAvailable;
    [ObservableProperty]
    public partial bool IsConnected { get; set; } = false;
    [ObservableProperty]
    public partial bool IsConnecting { get; set; } = false;
    [ObservableProperty]
    public partial string Data { get; set; } = string.Empty;
    [ObservableProperty]
    public partial bool IsLogging { get; set; } = false;
    [ObservableProperty]
    public partial string Ip { get; set; } = "127.0.0.1";
    [ObservableProperty]
    public partial SmartEye.IntersectionSource IntersectionSource { get; set; } = SmartEye.IntersectionSource.Gaze;
    [ObservableProperty]
    public partial bool UseFilteredData { get; set; } = false;
    [ObservableProperty]
    public partial string ConnectionButtonText { get; set; } = "Connect";

    public SmartEyeViewModel(SmartEyeClient smartEyeClient)
    {
        _smartEyeClient = smartEyeClient;
        _smartEyeClient.AvailabilityChanged += (s, e) =>
        {
            IsConnected = _smartEyeClient.IsConnected;
            OnPropertyChanged(nameof(IsAvailable));
        };

        _smartEyeClient.ConnectionChanged += (s, e) => IsConnected = e;
        _smartEyeClient.IntersectionChanged += (s, e) => SetIntersection(e);
    }

    #region Internal

    const string NO_INTERSECTION = "-";

    readonly SmartEyeClient _smartEyeClient;

    [RelayCommand]
    private async Task Configure()
    {
        IsConnecting = true;
        ConnectionButtonText = "Wait...";

        var isConnected = await _smartEyeClient.ConfigureAsync(Ip, IntersectionSource, UseFilteredData);
        IsConnecting = false;

        ConnectionButtonText = isConnected ? "Connected" : "Connect";
    }

    [RelayCommand]
    private void ToggleDataLogging()
    {
        IsLogging = _smartEyeClient.IsLogging;

        if (_smartEyeClient.IsLogging)
        {
            _smartEyeClient.Stop();
            Data = string.Empty;
        }
        else
        {
            _smartEyeClient.Start();
            Data = NO_INTERSECTION;
        }
    }

    partial void OnIsLoggingChanged(bool value)
    {
        _smartEyeClient.SetLoggingEnabled(value);
    }

    private void SetIntersection(SmartEye.Intersection intersection) =>
        Data = string.IsNullOrEmpty(intersection.Name)
            ? NO_INTERSECTION
            : intersection.Name;

    #endregion
}
