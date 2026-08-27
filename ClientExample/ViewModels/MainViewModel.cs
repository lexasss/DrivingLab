using CommunityToolkit.Mvvm.ComponentModel;

namespace ClientExample;

public partial class MainViewModel : ObservableObject
{
    public LeapMotionViewModel LeapMotion { get; }

    public MainViewModel(LeapMotionClient leapMotionClient)
    {
        _leapMotionClient = leapMotionClient;

        LeapMotion = new LeapMotionViewModel(leapMotionClient);

        HandleLeapMotionClient();
    }

    #region Internal

    readonly LeapMotionClient _leapMotionClient;

    private void HandleLeapMotionClient()
    {
        LeapMotion.IsConnected = _leapMotionClient.IsConnected;

        _leapMotionClient.ConnectionChanged += (s, e) => LeapMotion.IsConnected = e;
        _leapMotionClient.HandLocationChanged += (s, e) =>
        {
            if (_leapMotionClient.IsReading && LeapMotion.IsHandVisible)
                LeapMotion.SetData(e);
        };
        _leapMotionClient.HandVisibilityChanged += (s, e) =>
            {
                LeapMotion.IsHandVisible = e;
                if (_leapMotionClient.IsReading && !LeapMotion.IsHandVisible)
                    LeapMotion.ResetData();
            };
        _leapMotionClient.HandProximityChanged += (s, e) => LeapMotion.IsHandClose = e;

        LeapMotion.IsAvailable = _leapMotionClient.CheckAvailability();
    }

    #endregion
}
