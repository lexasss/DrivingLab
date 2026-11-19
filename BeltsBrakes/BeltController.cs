using TensionR;

namespace BeltsBrakes;

internal class BeltController
{
    public float GForceThreshold { get; set; } = -0.2f;
    public float GForceAbsMaximum { get; set; } = 3f;
    public int BeltTensionMaximum { get; set; } = 64;

    public BeltController(
        CarlaClient carlaClient,
        TensionRClient tensionRClient)
    {
        carlaClient.DataUpdated += (s, e) =>
        {
            if (e.Values.TryGetValue("gforce_longitudinal", out float gforce))
            {
                if (gforce < GForceThreshold)
                {
                    _beltTension = (int)Math.Clamp(
                        (-gforce + GForceThreshold) / GForceAbsMaximum * BeltTensionMaximum,
                        0, BeltTensionMaximum);
                    tensionRClient.SetTension(_beltTension, Side.Both);
                }
                else if (_beltTension > 0)
                {
                    _beltTension = 0;
                    tensionRClient.SetTension(_beltTension, Side.Both);
                }
            }
        };
    }

    #region Internal

    int _beltTension = 0;

    #endregion
}
