namespace ClientExample;

public partial class MainViewModel(
    LeapMotionViewModel leapMotionVm,
    SmartEyeViewModel smartEyeVm,
    SoundPlayerViewModel soundPlayerVm,
    ScreenViewModel screenVm,
    PointingViewModel pointingVm,
    TensionRViewModel tensionRVm,
    StreamDeckViewModel streamDeckVm)
{
    public LeapMotionViewModel LeapMotion { get; } = leapMotionVm;
    public SmartEyeViewModel SmartEye { get; } = smartEyeVm;
    public SoundPlayerViewModel SoundPlayer { get; } = soundPlayerVm;
    public ScreenViewModel Screen { get; } = screenVm;
    public PointingViewModel Pointing { get; } = pointingVm;
    public TensionRViewModel TensionR { get; } = tensionRVm;
    public StreamDeckViewModel StreamDeck { get; } = streamDeckVm;
}
