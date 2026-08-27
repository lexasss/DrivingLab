using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClientExample;

public partial class MainViewModel : ObservableObject
{
    #region Leap Motion
    public bool IsLeapMotionAvailable { get; private set; }
    [ObservableProperty]
    public partial bool IsLeapMotionConnected { get; private set; } = false;
    [ObservableProperty]
    public partial bool IsHandVisible { get; private set; } = false;
    [ObservableProperty]
    public partial bool IsHandClose { get; private set; } = false;
    [ObservableProperty]
    public partial string LeapMotionData { get; private set; } = "";
    #endregion

    public MainViewModel(LeapClient leapClient)
    {
        _leapClient = leapClient;

        HandleLeapMotionClient();
    }

    #region Internal

    const string WAITING_HAND = "waiting the hand...";

    readonly LeapClient _leapClient;

    [RelayCommand]
    private void ToggleLeapMotionDataReading()
    {
        if (_leapClient.IsReading)
        {
            _leapClient.Stop();
            LeapMotionData = string.Empty;
        }
        else
        {
            _leapClient.Start();
            LeapMotionData = WAITING_HAND;
        }
    }

    private void HandleLeapMotionClient()
    {
        IsLeapMotionConnected = _leapClient.IsConnected;

        _leapClient.ConnectionChanged += (s, e) => IsLeapMotionConnected = e;
        _leapClient.HandLocationChanged += (s, e) =>
        {
            if (_leapClient.IsReading && IsHandVisible)
                LeapMotionData = $"X = {e.X:F1}, Y = {e.Y:F1}, Z = {e.Z:F1}";
        };
        _leapClient.HandVisibilityChanged += (s, e) =>
            {
                IsHandVisible = e;
                if (_leapClient.IsReading && !IsHandVisible)
                    LeapMotionData = WAITING_HAND;
            };
        _leapClient.HandProximityChanged += (s, e) => IsHandClose = e;

        IsLeapMotionAvailable = _leapClient.CheckAvailability();
    }

    #endregion
}
