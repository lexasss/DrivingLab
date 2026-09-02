namespace ClientExample;

public partial class MainViewModel(
    LeapMotionViewModel leapMotionVm,
    SmartEyeViewModel smartEyeVm,
    SoundPlayerViewModel soundPlayerVm)
{
    public LeapMotionViewModel LeapMotion { get; } = leapMotionVm;
    public SmartEyeViewModel SmartEye { get; } = smartEyeVm;
    public SoundPlayerViewModel SoundPlayer { get; } = soundPlayerVm;
}
