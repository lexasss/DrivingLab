using Google.Protobuf.WellKnownTypes;

namespace Common;

public enum Ports
{
    LeapMotion = 30050,
    MyGaze = 30051,
    TobiiEyeX = 30052,
    SmartEye = 30053,
    SoundPlayer = 30054,
    Screen = 30055,
    Pointing = 30056,
    TensionR = 30057,
    StreamDeck = 30058,
    Driving = 30059,
}

public interface ILoggable
{
    string[] ToStringArray();
    string[] ToStringArray(int decimals);
}

public static class Constants
{
    public const int FILE_CHUNK_SIZE = 64 * 1024;

    public static Task<Empty> Empty { get; } =
        Task.FromResult(new Empty());
}

public partial class Bool
{
    public static Task<Bool> True { get; } = 
        Task.FromResult(new Bool()
        {
            Value = true
        });
    public static Task<Bool> False { get; } =
        Task.FromResult(new Bool()
        {
            Value = false
        });
    public static Task<Bool> From(bool value) =>
        Task.FromResult(new Bool() {
            Value = value
        });
    public Bool(bool value)
    {
        Value = value;
    }
}

public partial class String
{
    public static Task<String> Empty { get; } =
        Task.FromResult(new String()
        {
            Value = string.Empty
        });
    public static Task<String> From(string value) =>
        Task.FromResult(new String()
        {
            Value = value
        });
    public String(string value)
    {
        Value = value;
    }
}

public partial class Vector : ILoggable
{
    public readonly static Vector ZEROS = new()
    {
        X = 0,
        Y = 0,
        Z = 0
    };
    public readonly static Vector ONES = new()
    {
        X = 1,
        Y = 1,
        Z = 1
    };
    public static Vector Empty => new()
    {
        X = 0,
        Y = 0,
        Z = 0
    };
    public void Deconstruct(out double x, out double y, out double z)
    {
        x = X;
        y = Y;
        z = Z;
    }
    public string[] ToStringArray() => [
        X.ToString(),
        Y.ToString(),
        Z.ToString()
    ];
    public string[] ToStringArray(int decimals) => [
        X.ToString($"F{decimals}"),
        Y.ToString($"F{decimals}"),
        Z.ToString($"F{decimals}")
    ];
}

public partial class Point : ILoggable
{
    public readonly static Point ZEROS = new()
    {
        X = 0,
        Y = 0
    };
    public void Deconstruct(out double x, out double y)
    {
        x = X;
        y = Y;
    }
    public string[] ToStringArray() => [
        X.ToString(),
        Y.ToString()
    ];
    public string[] ToStringArray(int decimals) => [
        X.ToString($"F{decimals}"),
        Y.ToString($"F{decimals}")
    ];
}

public partial class Size : ILoggable
{
    public void Deconstruct(out double width, out double height)
    {
        width = Width;
        height = Height;
    }
    public string[] ToStringArray() => [
        Width.ToString(),
        Height.ToString()
    ];
    public string[] ToStringArray(int decimals) => [
        Width.ToString($"F{decimals}"),
        Height.ToString($"F{decimals}")
    ];
}