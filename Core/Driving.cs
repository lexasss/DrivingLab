namespace Driving;

public partial class Button
{
    public string[] ToStringArray() => [
        IsPressed ? "1" : "0",
    ];
}

public partial class RotaryButton
{
    public string[] ToStringArray() => [
        IsPressed ? "1" : "0",
        Rotation.ToString(),
    ];
    public string[] ToStringArray(int decimals) => [
        IsPressed ? "1" : "0",
        Rotation.ToString($"F{decimals}"),
    ];
}

public partial class RotaryTiltButton
{
    public string[] ToStringArray() => [
        IsPressed ? "1" : "0",
        Rotation.ToString(),
        Degrees.ToString(),
    ];
    public string[] ToStringArray(int decimals) => [
        IsPressed ? "1" : "0",
        Rotation.ToString($"F{decimals}"),
        Degrees.ToString($"F{decimals}"),
    ];
}

public partial class Data : Common.ILoggable
{
    public string[] ToStringArray() => [
        WheelRotation.ToString(),
        ThrottlePedal.ToString(),
        BrakePedal.ToString(),
        ActiveThrottlePedal.ToString(),
        ActiveBrakePedal.ToString(),
        TopLeftButton1.ToString(),
        TopLeftButton2.ToString(),
        TopLeftButton3.ToString(),
        TopRightButton1.ToString(),
        TopRightButton2.ToString(),
        TopRightButton3.ToString(),
        BottomLeftButton1.ToString(),
        BottomLeftButton2.ToString(),
        BottomRightButton1.ToString(),
        BottomRightButton2.ToString(),
        LeftPaddleShifter.ToString(),
        RightPaddleShifter.ToString(),
        ..RotaryButton.ToStringArray(),
        ..RotaryTiltButton.ToStringArray(),
    ];
    public string[] ToStringArray(int decimals)
    {
        string format = $"F{decimals}";
        return [
            WheelRotation.ToString(format),
            ThrottlePedal.ToString(format),
            BrakePedal.ToString(format),
            ActiveThrottlePedal.ToString(format),
            ActiveBrakePedal.ToString(format),
            ..TopLeftButton1.ToStringArray(),
            ..TopLeftButton2.ToStringArray(),
            ..TopLeftButton3.ToStringArray(),
            ..TopRightButton1.ToStringArray(),
            ..TopRightButton2.ToStringArray(),
            ..TopRightButton3.ToStringArray(),
            ..BottomLeftButton1.ToStringArray(),
            ..BottomLeftButton2.ToStringArray(),
            ..BottomRightButton1.ToStringArray(),
            ..BottomRightButton2.ToStringArray(),
            ..LeftPaddleShifter.ToStringArray(),
            ..RightPaddleShifter.ToStringArray(),
            ..RotaryButton.ToStringArray(decimals),
            ..RotaryTiltButton.ToStringArray(decimals),
        ];
    }
}
