namespace BeltsBrakes;

internal class ScreenController
{
    public ScreenController(
        CarlaClient carlaClient,
        ScreenClient screenClient)
    {
        _screenClient = screenClient;

        var screens = _screenClient.GetScreens().Items.ToArray();
        _leftMirror = screens.FirstOrDefault(item => item.Name == "Left mirror") ?? screens.FirstOrDefault();

        _screenClient.MediaHidden += (s, e) =>
        {
            _mediaId = null;
        }; 

        carlaClient.DataUpdated += (s, e) =>
        {
            if (_leftMirror == null)
                return;
            if (!_canShowMedia)
                return;

            if (e.Values.TryGetValue("speed", out float speed))
            {
                var newSignState = CalculateSignState(speed);
                if (newSignState != _lastSignState)
                {
                    if (newSignState == SingState.Critical)
                    {
                        _ = Show("critical-sign.png");
                    }
                    else if (newSignState == SingState.Warning && _lastSignState == SingState.None)
                    {
                        _ = Show("warning-sign.png");
                    }
                    else
                    {
                        _ = Show(null);
                    }

                    _lastSignState = newSignState;
                }
            }
        };
    }

    enum SingState
    {
        None,
        Warning,
        Critical
    }

    const float WarningSpeedThreshold = 15f;
    const float CriticalSpeedThreshold = 19f;
    const float Histeresis = 1f;

    readonly ScreenClient _screenClient;

    Screen.Screen? _leftMirror;
    SingState _lastSignState = SingState.None;
    string? _mediaId;
    bool _canShowMedia = true;

    private SingState CalculateSignState(float speed)
    {
        if (_lastSignState == SingState.Critical)
            return GetSignState(speed, criticalSpeedThreshold: CriticalSpeedThreshold - Histeresis);
        else if (_lastSignState == SingState.Warning)
            return GetSignState(speed, warningSpeedThreshold: WarningSpeedThreshold - Histeresis);
        else
            return GetSignState(speed);
    }

    private SingState GetSignState(
        float speed,
        float warningSpeedThreshold = WarningSpeedThreshold,
        float criticalSpeedThreshold = CriticalSpeedThreshold)
    {
        if (speed >= criticalSpeedThreshold)
            return SingState.Critical;
        else if (speed >= warningSpeedThreshold)
            return SingState.Warning;
        else
            return SingState.None;
    }

    private async Task Show(string? filename)
    {
        if (_leftMirror == null)
            return;

        _canShowMedia = false;

        if (_mediaId != null)
        {
            _screenClient.Hide(_mediaId);
            _mediaId = null;
        }

        while (_mediaId != null)
        {
            await Task.Delay(20);
        }

        if (filename != null)
        {
            _mediaId = await _screenClient.Show(
                filename,
                _leftMirror.Id,
                new Common.Point() { X = 50, Y = 50 },
                null, 5000);
        }

        _canShowMedia = true;
    }
}
