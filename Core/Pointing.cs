namespace Pointing;

public static class Controls
{
    public const string Button = "BTN";
    public const string Slider = "SLD";
    public const string PointOfView = "POV";
}

public partial class Button
{
    public string[] ToStringArray() => [
        Name,
        Offset.ToString(),
        Id.ToString(),
        IsPressed ? "1" : "0"
    ];
}

public partial class Slider
{
    public string[] ToStringArray() => [
        Name,
        Offset.ToString(),
        Id.ToString(),
        Type.ToString(),
        Value.ToString(),
    ];
    public string[] ToStringArray(int decimals) => [
        Name,
        Offset.ToString(),
        Id.ToString(),
        Type.ToString(),
        Value.ToString($"F{decimals}"),
    ];
}

public partial class PointOfView
{
    public string[] ToStringArray() => [
        Name,
        Offset.ToString(),
        Id.ToString(),
        IsPressed ? "1" : "0",
        Degrees.ToString(),
    ];
    public string[] ToStringArray(int decimals) => [
        Name,
        Offset.ToString(),
        Id.ToString(),
        IsPressed ? "1" : "0",
        Degrees.ToString($"F{decimals}"),
    ];
}

public partial class Data
{
    public string[] ToStringArray() => [
        ..Point.ToStringArray(),
        ..Rotation.ToStringArray(),
        ..Velocity.ToStringArray(),
        ..AngularVelocity.ToStringArray(),
        ..Acceleration.ToStringArray(),
        ..AngularAcceleration.ToStringArray(),
        ..Force.ToStringArray(),
        ..Torque.ToStringArray(),
        ..Buttons.SelectMany(v => v.ToStringArray()),
        ..Sliders.SelectMany(v => v.ToStringArray()),
        ..PointOfViews.SelectMany(v => v.ToStringArray()),
    ];
    public string[] ToStringArray(int decimals) => [
        ..Point.ToStringArray(decimals),
        ..Rotation.ToStringArray(decimals),
        ..Velocity.ToStringArray(decimals),
        ..AngularVelocity.ToStringArray(decimals),
        ..Acceleration.ToStringArray(decimals),
        ..AngularAcceleration.ToStringArray(decimals),
        ..Force.ToStringArray(decimals),
        ..Torque.ToStringArray(decimals),
        ..Buttons.SelectMany(v => v.ToStringArray()),
        ..Sliders.SelectMany(v => v.ToStringArray(decimals)),
        ..PointOfViews.SelectMany(v => v.ToStringArray(decimals)),
    ];
}
