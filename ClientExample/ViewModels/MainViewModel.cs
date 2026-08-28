using CommunityToolkit.Mvvm.ComponentModel;

namespace ClientExample;

public partial class MainViewModel : ObservableObject
{
    public LeapMotionViewModel LeapMotion { get; }

    public MainViewModel(LeapMotionViewModel leapMotionVm)
    {
        LeapMotion = leapMotionVm;
    }
}
