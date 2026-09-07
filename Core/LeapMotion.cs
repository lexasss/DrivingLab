namespace LeapMotion;

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
