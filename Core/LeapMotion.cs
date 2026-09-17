namespace LeapMotion;

public partial class Sample : Common.ILoggable
{
    public string[] ToStringArray() => [
        ..Palm.ToStringArray(),
        ..Fingertips.SelectMany(ft => ft.ToStringArray())
    ];
    public string[] ToStringArray(int decimals) => [
        ..Palm.ToStringArray(decimals),
        ..Fingertips.SelectMany(ft => ft.ToStringArray(decimals))
    ];
}
