namespace Gaze;

public partial class Sample
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
}
