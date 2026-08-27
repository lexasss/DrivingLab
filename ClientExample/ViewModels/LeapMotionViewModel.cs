using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

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
    public partial string Data { get; set; } = "";
    [ObservableProperty]
    public partial bool IsStreaming { get; set; } = false;
    [ObservableProperty]
    public partial LeapMotionConfig Config { get; set; } = LeapMotionConfig.Default;

    public LeapMotionViewModel(LeapMotionClient leapMotionClient)
    {
        _leapMotionClient = leapMotionClient;
    }

    public void SetData(LeapMotionClient.Point pt) => 
        Data = $"X = {pt.X:F1}, Y = {pt.Y:F1}, Z = {pt.Z:F1}";

    public void ResetData() => 
        Data = WAITING_HAND;

    #region Internal

    const string WAITING_HAND = "waiting a hand to appear...";

    readonly LeapMotionClient _leapMotionClient;

    [RelayCommand]
    private void ToggleDataReading()
    {
        if (_leapMotionClient.IsReading)
        {
            _leapMotionClient.Stop();
            IsStreaming = false;
            Data = string.Empty;
        }
        else
        {
            _leapMotionClient.Start();
            IsStreaming = true;
            Data = WAITING_HAND;
        }
    }

    partial void OnConfigChanged(LeapMotionConfig value)
    {
        _leapMotionClient.Configure(value);
    }

    #endregion
}
