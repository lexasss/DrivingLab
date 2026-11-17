namespace Carla;

public partial class Data : Common.ILoggable
{
    public string[] ToStringArray() => Values.Select(kv => kv.Value.ToString()).ToArray();
    public string[] ToStringArray(int decimals)
    {
        string format = $"F{decimals}";
        return Values.Select(kv => kv.Value.ToString(format)).ToArray();
    }
}
