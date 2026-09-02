namespace Common;

public enum Ports
{
    LeapMotion = 30050,
    MyGaze = 30051,
    TobiiEyeX = 30052,
    SmartEye = 30053,
    SoundPlayer = 30054,
    Screen = 30055
}

public partial class Vector
{
    public readonly static Vector ZEROS = new() { X = 0, Y = 0, Z = 0 };
    public readonly static Vector ONES = new() { X = 1, Y = 1, Z = 1 };
    public string[] ToStringArray() => [
        X.ToString(),
        Y.ToString(),
        Z.ToString()
    ];
}

public partial class Point
{
    public void Deconstruct(out double x, out double y)
    {
        x = X;
        y = Y;
    }
}


public partial class Size
{
    public void Deconstruct(out double width, out double height)
    {
        width = Width;
        height = Height;
    }
}