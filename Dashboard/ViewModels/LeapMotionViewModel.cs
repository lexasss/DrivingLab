using CommunityToolkit.Mvvm.ComponentModel;

namespace Dashboard;

public partial class LeapMotionViewModel : ObservableObject
{
    public bool IsAvailable => _client.IsAvailable;
    [ObservableProperty]
    public partial bool IsConnected { get; set; } = false;
    [ObservableProperty]
    public partial bool IsHandVisible { get; set; } = false;
    [ObservableProperty]
    public partial bool IsHandClose { get; set; } = false;
    [ObservableProperty]
    public partial string Data { get; set; } = string.Empty;
    [ObservableProperty]
    public partial bool IsStreaming { get; set; } = false;
    [ObservableProperty]
    public partial bool IsLogging { get; set; } = false;
    [ObservableProperty]
    public partial LeapMotion.ConfigType Config { get; set; } = LeapMotion.ConfigType.Default;

    public LeapMotionViewModel(LeapMotionClient leapMotionClient)
    {
        _client = leapMotionClient;
        _client.AvailabilityChanged += (s, e) =>
        {
            IsConnected = _client.IsConnected;
            IsStreaming = _client.IsStreamingData;
            IsLogging = _client.IsLogging;

            OnPropertyChanged(nameof(IsAvailable));
        };

        _client.ConnectionChanged += (s, e) => IsConnected = e;
        _client.HandLocationChanged += (s, e) =>
        {
            if (_client.IsStreamingData && IsHandVisible)
                SetData(e);
        };
        _client.HandVisibilityChanged += (s, e) =>
        {
            IsHandVisible = e;
            if (_client.IsStreamingData && !IsHandVisible)
                ResetData();
        };
        _client.HandProximityChanged += (s, e) => IsHandClose = e;
    }

    #region Internal

    const string WAITING_HAND = "waiting a hand to appear...";

    readonly LeapMotionClient _client;

    partial void OnIsStreamingChanging(bool oldValue, bool newValue)
    {
        if (oldValue)
        {
            _client.Stop();
            Data = string.Empty;
        }
        else
        {
            _client.Start();
            Data = WAITING_HAND;
        }
    }

    partial void OnIsLoggingChanged(bool value)
    {
        _client.SetLoggingEnabled(value);
    }

    partial void OnConfigChanged(LeapMotion.ConfigType value)
    {
        _client.Configure(value);
    }

    private void SetData(LeapMotion.Sample pt) =>
        Data = $"X = {pt.Palm.X:F1}\nY = {pt.Palm.Y:F1}\nZ = {pt.Palm.Z:F1}";

    private void ResetData() =>
        Data = WAITING_HAND;

    #endregion
}
