using TensionR;

namespace BeltsBrakes;

internal class BeltController
{
    public BeltController(
        CarlaClient carlaClient,
        TensionRClient tensionRClient)
    {
        carlaClient.DataUpdated += (s, e) =>
        {
            if (e.Values.TryGetValue("gforce_longitudinal", out float gforce))
            {
                if (gforce < -0.5)
                {
                    _beltTension = (int)Math.Clamp((-gforce - 0.5) / 6 * 64, 0, 64);
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
