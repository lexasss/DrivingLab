namespace ClientExample;

public partial class MainViewModel(
    LeapMotionViewModel leapMotionVm,
    SmartEyeViewModel smartEyeVm,
    SoundPlayerViewModel soundPlayerVm,
    ScreenViewModel screenVm)
{
    public LeapMotionViewModel LeapMotion { get; } = leapMotionVm;
    public SmartEyeViewModel SmartEye { get; } = smartEyeVm;
    public SoundPlayerViewModel SoundPlayer { get; } = soundPlayerVm;
    public ScreenViewModel Screen { get; } = screenVm;
}
