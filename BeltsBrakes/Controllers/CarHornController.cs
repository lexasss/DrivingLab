namespace BeltsBrakes.Controllers;

internal class CarHornController
{
    public CarHornController(
        SoundPlayerClient soundPlayerClient,
        WheelAndPedalsClient wnpClient)
    {
        _soundPlayerClient = soundPlayerClient;
        _soundPlayerClient.PlaybackFinished += (s, e) =>
        {
            _isPlayingHornSound = false;
        };

        _wnpClient = wnpClient;
        _wnpClient.DataUpdated += (s, e) =>
        {
            if (_wnpClient.IsStreamingData)
            {
                if (e.TopRightButton2.IsPressed && !_isButtonPressed)
                {
                    if (_soundPlayerClient.IsAvailable && !_isPlayingHornSound)
                    {
                        _ = _soundPlayerClient.PlayFile("car-horn.mp3");
                        _isPlayingHornSound = true;
                    }
                }

                _isButtonPressed = e.TopRightButton2.IsPressed;
            }
        };
    }

    #region Internal

    readonly SoundPlayerClient _soundPlayerClient;
    readonly WheelAndPedalsClient _wnpClient;

    bool _isButtonPressed = false;
    bool _isPlayingHornSound = false;

    #endregion
}
