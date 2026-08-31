namespace ClientExample;

public partial class MainViewModel(
    LeapMotionViewModel leapMotionVm,
    SmartEyeViewModel smartEyeVm)
{
    public LeapMotionViewModel LeapMotion { get; } = leapMotionVm;
    public SmartEyeViewModel SmartEye { get; } = smartEyeVm;
}
