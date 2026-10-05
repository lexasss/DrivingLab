using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Dashboard;

public partial class SmartEyeViewModel : ObservableObject
{
    public bool IsAvailable => _client.IsAvailable;
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
    public partial SmartEye.IntersectionSource IntersectionSource { get; set; } = 
        SmartEye.IntersectionSource.Gaze;
    [ObservableProperty]
    public partial bool UseFilteredData { get; set; } = false;
    [ObservableProperty]
    public partial string ConnectionButtonText { get; set; } = "Connect";

    public SmartEyeViewModel(GrpcClients.SmartEyeClient smartEyeClient)
    {
        _client = smartEyeClient;
        _client.AvailabilityChanged += (s, e) =>
        {
            IsConnected = _client.IsConnected;
            IsLogging = _client.IsLogging;

            OnPropertyChanged(nameof(IsAvailable));
        };

        _client.ConnectionChanged += (s, e) => IsConnected = e;
        _client.IntersectionChanged += (s, e) => SetIntersection(e);
    }

    #region Internal

    const string NO_INTERSECTION = "-";

    readonly GrpcClients.SmartEyeClient _client;

    [RelayCommand]
    private async Task Configure()
    {
        IsConnecting = true;
        ConnectionButtonText = "Wait...";

        var isConnected = await _client.ConfigureAsync(
            Ip, IntersectionSource, UseFilteredData);
        IsConnecting = false;

        ConnectionButtonText = isConnected ? "Connected" : "Connect";
    }

    [RelayCommand]
    private void ToggleDataLogging()
    {
        IsLogging = _client.IsLogging;

        if (_client.IsLogging)
        {
            _client.Stop();
            Data = string.Empty;
        }
        else
        {
            _client.Start();
            Data = NO_INTERSECTION;
        }
    }

    partial void OnIsLoggingChanged(bool value)
    {
        _client.SetLoggingEnabled(value);
    }

    private void SetIntersection(SmartEye.Intersection intersection) =>
        Data = string.IsNullOrEmpty(intersection.Name)
            ? NO_INTERSECTION
            : intersection.Name;

    #endregion
}
