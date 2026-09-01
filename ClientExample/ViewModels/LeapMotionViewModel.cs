using CommunityToolkit.Mvvm.ComponentModel;

namespace ClientExample;

public partial class LeapMotionViewModel : ObservableObject
{
    public bool IsAvailable { get; set; }
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
        _leapMotionClient = leapMotionClient;

        IsConnected = _leapMotionClient.IsConnected;
        IsStreaming = _leapMotionClient.IsReading;

        _leapMotionClient.ConnectionChanged += (s, e) => IsConnected = e;
        _leapMotionClient.HandLocationChanged += (s, e) =>
        {
            if (_leapMotionClient.IsReading && IsHandVisible)
                SetData(e);
        };
        _leapMotionClient.HandVisibilityChanged += (s, e) =>
        {
            IsHandVisible = e;
            if (_leapMotionClient.IsReading && !IsHandVisible)
                ResetData();
        };
        _leapMotionClient.HandProximityChanged += (s, e) => IsHandClose = e;

        IsAvailable = _leapMotionClient.CheckAvailability();
    }

    #region Internal

    const string WAITING_HAND = "waiting a hand to appear...";

    readonly LeapMotionClient _leapMotionClient;

    partial void OnIsStreamingChanged(bool value)
    {
        if (_leapMotionClient.IsReading)
        {
            _leapMotionClient.Stop();
            Data = string.Empty;
        }
        else
        {
            _leapMotionClient.Start();
            Data = WAITING_HAND;
        }
    }

    partial void OnIsLoggingChanged(bool value)
    {
        _leapMotionClient.SetLoggingEnabled(value);
    }

    partial void OnConfigChanged(LeapMotion.ConfigType value)
    {
        _leapMotionClient.Configure(value);
    }

    private void SetData(LeapMotion.Sample pt) =>
        Data = $"X = {pt.Palm.X:F1}\nY = {pt.Palm.Y:F1}\nZ = {pt.Palm.Z:F1}";

    private void ResetData() =>
        Data = WAITING_HAND;

    #endregion
}
