namespace LeapMotion;

public static class Events
{
    public const string IS_CONNECTED = "isConnected";
    public const string IS_HAND_VISIBLE = "isHandVisible";
    public const string IS_HAND_CLOSE = "isHandClose";
}

public partial class Sample
{
    public string[] ToStringArray() => [
        ..Palm.ToStringArray(),
        ..Fingertips.SelectMany(ft => ft.ToStringArray()).ToArray()
    ];
    public string[] ToStringArray(int decimals) => [
        ..Palm.ToStringArray(decimals),
        ..Fingertips.SelectMany(ft => ft.ToStringArray(decimals)).ToArray()
    ];
}
