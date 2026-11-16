namespace Carla;

public partial class Data : Common.ILoggable
{
    public string[] ToStringArray() => [
        Time.ToString(),
    ];
    public string[] ToStringArray(int decimals)
    {
        string format = $"F{decimals}";
        return [
            Time.ToString(format),
        ];
    }
}
