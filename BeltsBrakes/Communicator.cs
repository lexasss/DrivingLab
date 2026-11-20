namespace BeltsBrakes;

internal class Communicator
{
    public COMUtils.Port? BeltComPort { get; set; }
    public List<COMUtils.Port> ComPorts { get; } = [];

    public Communicator(
        CarlaClient carlaClient,
        TensionRClient tensionRClient,
        ScreenClient screenClient,
        WheelAndPedalsClient drivingClient,
        SoundPlayerClient soundPlayerClient)
    {
        _carla = carlaClient;
        _screen = screenClient;
        _driving = drivingClient;
        _soundPlayer = soundPlayerClient;

        _tensionR = tensionRClient;
        _tensionR.ConnectionChanged += (s, e) =>
        {
            _isBeltConnected = e;
        };
        _tensionR.EnabledChanged += (s, e) =>
        {
            _isBeltEnabled = e;
        };
        _tensionR.CalibrationChanged += (s, e) =>
        {
            _isBeltCalibrated = e;
        };

        try
        {
            Wait(() => carlaClient.IsAvailable && tensionRClient.IsAvailable);
        }
        catch
        {
            throw new InvalidOperationException("One or more required clients are not available.");
        }

        foreach (var port in _comUtils.Ports)
            ComPorts.Add(port);

        BeltComPort = ComPorts.FirstOrDefault(port =>
            port.Description?.Contains(TENSIONR_PORT_DESCRIPTION) ?? false);
    }

    public bool PrepareBelt()
    {
        if (BeltComPort == null)
            return false;

        try
        {
            _tensionR.Connect(BeltComPort.Name);
            Wait(() => _isBeltConnected);
            Thread.Sleep(100);

            _tensionR.Start();
            Wait(() => _isBeltEnabled, 1000);
            Thread.Sleep(100);

            _tensionR.Calibrate();
            Wait(() => _isBeltCalibrated, 10000);
            Thread.Sleep(100);
        }
        catch
        {
            return false;
        }

        return _isBeltEnabled;
    }

    public void Start()
    {
        _carla.SetStreamingFields(["speed", "gforce_longitudinal"]);
        _carla.Start();
        _driving.Start();
    }

    public void Stop()
    {
        _tensionR.Stop();
        _carla.Stop();
        _driving.Stop();
        _screen.Hide();     // hides all visible media
        _soundPlayer.Stop();
    }

    #region Internal

    const string TENSIONR_PORT_DESCRIPTION = "Silicon Labs CP210x USB to UART Bridge";

    readonly CarlaClient _carla;
    readonly TensionRClient _tensionR;
    readonly ScreenClient _screen;
    readonly WheelAndPedalsClient _driving;
    readonly SoundPlayerClient _soundPlayer;

    readonly COMUtils _comUtils = new();

    bool _isBeltConnected = false;
    bool _isBeltCalibrated = false;
    bool _isBeltEnabled = false;

    private static void Wait(Func<bool> condition, int timeoutMs = 5000)
    {
        var startTime = DateTime.Now;
        while (!condition())
        {
            if ((DateTime.Now - startTime).TotalMilliseconds > timeoutMs)
                throw new TimeoutException($"Condition not met within {(double)timeoutMs/1000:F1} seconds.");
            Thread.Sleep(100);
        }
    }

    #endregion
}
