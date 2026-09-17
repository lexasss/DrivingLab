namespace Gaze;

public partial class Sample : Common.ILoggable
{
    public string[] ToStringArray() => [
        Timestamp.ToString(),
        EyeX.ToString(),
        EyeY.ToString(),
        EyeXL.ToString(),
        EyeYL.ToString(),
        EyeXR.ToString(),
        EyeYR.ToString(),
        CamXL.ToString(),
        CamYL.ToString(),
        CamXR.ToString(),
        CamYR.ToString(),
        ValidEye.ToString()
    ];
    public string[] ToStringArray(int decimals)
    {
        string format = $"F{decimals}";
        return [
            Timestamp.ToString(),
            EyeX.ToString(format),
            EyeY.ToString(format),
            EyeXL.ToString(format),
            EyeYL.ToString(format),
            EyeXR.ToString(format),
            EyeYR.ToString(format),
            CamXL.ToString(format),
            CamYL.ToString(format),
            CamXR.ToString(format),
            CamYR.ToString(format),
            ValidEye.ToString()
        ];
    }
}
