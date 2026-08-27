using Leap;
using Microsoft.Extensions.Logging;

namespace Server.LeapMotion;

internal class LeapM: IDisposable
{
    public bool IsUHRelated { get; set; } = false;
    public bool IsServiceRunning => _controller?.IsServiceConnected ?? false;
    public bool IsConnected => _controller?.IsConnected ?? false;
    public Device? Info { get; private set; }
    public DeviceList? Devices => _controller?.Devices;

    public event EventHandler<string>? ServiceStatusChanged;
    public event EventHandler<bool>? ConnectionChanged;
    public event EventHandler<bool>? HandVisibilityChanged;
    public event EventHandler<Vector>? HandLocationChanged;
    public event EventHandler<bool>? HandProximityChanged;

    public LeapM(ILogger<LeapMotionService> logger) 
    {
        _logger = logger;

        _controller = new Controller();
        
        _controller.SetPolicy(Controller.PolicyFlag.POLICY_DEFAULT);

        _controller.Connect += OnServiceConnect;
        _controller.Disconnect += OnServiceDisconnect;
        _controller.Device += OnConnect;
        _controller.DeviceLost += OnDisconnect;
        _controller.DeviceFailure += OnDeviceFailure;
        _controller.LogMessage += OnLogMessage;
    }

    public void Run()
    {
        _controller?.FrameReady += OnFrame;
    }

    public void Dispose()
    {
        _controller?.Dispose();
        _controller = null;
    }

    // Internal methods

    readonly ILogger<LeapMotionService> _logger;

    Controller? _controller = null;
    bool _handVisible = false;
    bool _isHandInSetUpBox = false;

    readonly Vector SETUP_BOX_1 = new(-40, 50, -40);
    readonly Vector SETUP_BOX_2 = new(40, 400, 40);
    readonly Vector UH_SETUP_BOX_1 = new(-40, 50, 80);
    readonly Vector UH_SETUP_BOX_2 = new(40, 400, 160);

    const int LEAP_TO_UH_X = 0;   // depends on the device: the palm center point may a bit offset from the very center
    const int LEAP_TO_UH_Y = 121;

    private static void PrintDeviceInfo(Device device)
    {
        Console.WriteLine($"[LEAP] device {device.SerialNumber}:");
        Console.WriteLine($"[LEAP]   baseline = {device.Baseline}");
        Console.WriteLine($"[LEAP]   view angle");
        Console.WriteLine($"[LEAP]     horizontal  = {device.HorizontalViewAngle}");
        Console.WriteLine($"[LEAP]     vertical = {device.VerticalViewAngle}");
        Console.WriteLine($"[LEAP]   lightning bad = {device.IsLightingBad}");
        Console.WriteLine($"[LEAP]   smudged = {device.IsSmudged}");
        Console.WriteLine($"[LEAP]   streaming = {device.IsStreaming}");
        Console.WriteLine($"[LEAP]   range = {device.Range}");
        Console.WriteLine($"[LEAP]   type = {device.Type}");
    }

    private bool IsHandInSetUpBox(Vector aPos)
    {
        var box1 = IsUHRelated ? UH_SETUP_BOX_1 : SETUP_BOX_1;
        var box2 = IsUHRelated ? UH_SETUP_BOX_2 : SETUP_BOX_2;
        return aPos.x > box1.x && aPos.x < box2.x
            && aPos.y > box1.y && aPos.y < box2.y
            && aPos.z > box1.z && aPos.z < box2.z;
    }

    private void OnConnect(object? sender, DeviceEventArgs args)
    {
        _logger.LogInformation($"[LEAP] Connected to {args.Device.SerialNumber}");

        Info = args.Device;
        //PrintDeviceInfo(Info);

        ConnectionChanged?.Invoke(this, true);
    }

    private void OnDisconnect(object? sender, DeviceEventArgs args)
    {
        _logger.LogInformation("[LEAP] Disconnected");
        ConnectionChanged?.Invoke(this, false);
    }

    private void OnFrame(object? sender, FrameEventArgs args)
    {
        Frame frame = args.frame;

        if (frame.Hands.Count > 0)
        {
            if (!_handVisible)
            {
                _handVisible = true;
                HandVisibilityChanged?.Invoke(this, _handVisible);
            }

            var palmPos = frame.Hands[0].PalmPosition;

            var point = !IsUHRelated ? palmPos : new Vector(
                    (palmPos.x + LEAP_TO_UH_X) / 1000,
                    (-palmPos.z + LEAP_TO_UH_Y) / 1000,
                    palmPos.y / 1000
                );

            HandLocationChanged?.Invoke(this, point);

            var isHandInSetUpBox = IsHandInSetUpBox(palmPos);
            if (isHandInSetUpBox && !_isHandInSetUpBox)
            {
                HandProximityChanged?.Invoke(this, true);
            }
            else if(!isHandInSetUpBox && _isHandInSetUpBox)
            {
                HandProximityChanged?.Invoke(this, false);
            }

            _isHandInSetUpBox = isHandInSetUpBox;
        }
        else if (_handVisible)
        {
            _handVisible = false;
            HandVisibilityChanged?.Invoke(this, _handVisible);
        }
    }

    private void OnServiceConnect(object? sender, ConnectionEventArgs args)
    {
        _logger.LogInformation("[LEAP] Service Connected");

        ServiceStatusChanged?.Invoke(this, "connected");
    }

    private void OnServiceDisconnect(object? sender, ConnectionLostEventArgs args)
    {
        _logger.LogInformation("[LEAP] Service Disconnected");

        ServiceStatusChanged?.Invoke(this, "disconnected");
    }

    private void OnDeviceFailure(object? sender, DeviceFailureEventArgs args)
    {
        _logger.LogError("[LEAP] Device Error");
        _logger.LogError($"[LEAP]   PNP ID: {args.DeviceSerialNumber}");
        _logger.LogError($"[LEAP]   Failure message: {args.ErrorMessage}");
    }

    private void OnLogMessage(object? sender, LogEventArgs args)
    {
        var (type, level) = args.severity switch
        {
            MessageSeverity.MESSAGE_CRITICAL => ("Critical", LogLevel.Error),
            MessageSeverity.MESSAGE_WARNING => ("Warning", LogLevel.Warning),
            MessageSeverity.MESSAGE_INFORMATION => ("Info", LogLevel.Information),
            _ => ("Unknown", LogLevel.Debug),
        };

        _logger.Log(level, $"[LEAP] [{type}] {args.type}: {args.message}");
    }
}
