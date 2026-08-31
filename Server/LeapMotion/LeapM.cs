using Leap;
using Microsoft.Extensions.Logging;

namespace Server.LeapMotion;

internal class LeapM: IDisposable
{
    public DeviceList? Devices => _controller?.Devices;
    public bool IsServiceRunning => _controller?.IsServiceConnected ?? false;
    public bool IsConnected => _controller?.IsConnected ?? false;
    public Device? Info { get; private set; }

    public event EventHandler<string>? ServiceStatusChanged;
    public event EventHandler<bool>? ConnectionChanged;
    public event EventHandler<bool>? HandVisibilityChanged;
    public event EventHandler<global::LeapMotion.Sample>? HandLocationChanged;
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

    public void ConfigureForUltrahaptics()
    {
        _proximityCorner1 = UH_CORNER_1;
        _proximityCorner2 = UH_CORNER_2;
        _translation = UH_TRANSLATION;
        _scale = UH_SCALE;
    }

    public void SetProximityBox(Common.Vector? corner1 = null, Common.Vector? corner2 = null)
    {
        if (corner1 == null || corner2 == null)
        {
            _proximityCorner1 = CLOSE_CORNER_1;
            _proximityCorner2 = CLOSE_CORNER_2;
        }
    }

    public void SetTransform(Common.Vector? translation = null, Common.Vector? scale = null)
    {
        _translation = translation ?? Common.Vector.ZEROS;
        _scale = scale ?? Common.Vector.ONES;
    }

    #region Internal

    const int LEAP_TO_UH_X = 0;   // depends on the device: the palm center point may a bit offset from the very center
    const int LEAP_TO_UH_Y = -121;  // negative because of -1 for Y scale

    readonly static Common.Vector CLOSE_CORNER_1 = new() { X = -40, Y = 50, Z = -40 };
    readonly static Common.Vector CLOSE_CORNER_2 = new() { X = 40, Y = 400, Z = 40 };
    readonly static Common.Vector UH_CORNER_1 = new() { X = -40, Y = 50, Z = 80 };
    readonly static Common.Vector UH_CORNER_2 = new() { X = 40, Y = 400, Z = 160 };
    readonly static Common.Vector UH_TRANSLATION = new() { X = LEAP_TO_UH_X, Y = LEAP_TO_UH_Y, Z = 0 };
    readonly static Common.Vector UH_SCALE = new() { X = 0.001, Y = -0.001, Z = 0.001 };

    readonly ILogger<LeapMotionService> _logger;

    Controller? _controller = null;
    bool _isHandVisible = false;
    bool _isHandClose = false;

    Common.Vector _proximityCorner1 = CLOSE_CORNER_1;
    Common.Vector _proximityCorner2 = CLOSE_CORNER_2;
    Common.Vector _translation = Common.Vector.ZEROS;
    Common.Vector _scale = Common.Vector.ONES;


    private void PrintDeviceInfo(Device device)
    {
        _logger.LogInformation("[LEAP]   baseline = {baseline}", device.Baseline);
        _logger.LogInformation("[LEAP]   view angle");
        _logger.LogInformation("[LEAP]     horizontal  = {viewAngle}", device.HorizontalViewAngle);
        _logger.LogInformation("[LEAP]     vertical = {viewAngle}", device.VerticalViewAngle);
        _logger.LogInformation("[LEAP]   is lightning bad = {isLightBad}", device.IsLightingBad);
        _logger.LogInformation("[LEAP]   is smudged = {isSmudged}", device.IsSmudged);
        _logger.LogInformation("[LEAP]   is streaming = {isStreaming}", device.IsStreaming);
        _logger.LogInformation("[LEAP]   range = {range}", device.Range);
        _logger.LogInformation("[LEAP]   type = {type}", device.Type);
    }

    private bool IsHandClose(Vector aPos)
    {
        return aPos.x > _proximityCorner1.X && aPos.x < _proximityCorner2.X
            && aPos.y > _proximityCorner1.Y && aPos.y < _proximityCorner2.Y
            && aPos.z > _proximityCorner1.Z && aPos.z < _proximityCorner2.Z;
    }

    private void OnConnect(object? sender, DeviceEventArgs args)
    {
        _logger.LogInformation("[LEAP] Connected to {id}", args.Device.SerialNumber);

        Info = args.Device;

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
            if (!_isHandVisible)
            {
                _isHandVisible = true;
                HandVisibilityChanged?.Invoke(this, _isHandVisible);
            }

            var palmPos = frame.Hands[0].PalmPosition;

            var sample = new global::LeapMotion.Sample()
            {
                Palm = new Common.Vector()
                {
                    X = (palmPos.x + _translation.X) * _scale.X,
                    Y = (palmPos.z + _translation.Y) * _scale.Y,
                    Z = (palmPos.y + _translation.Z) * _scale.Z
                }
            };
            foreach (var finger in frame.Hands[0].Fingers)
            {
                sample.Fingertips.Add(new Common.Vector()
                {
                    X = (finger.TipPosition.x + _translation.X) * _scale.X,
                    Y = (finger.TipPosition.z + _translation.Y) * _scale.Y,
                    Z = (finger.TipPosition.y + _translation.Z) * _scale.Z
                });
            }

            HandLocationChanged?.Invoke(this, sample);

            var isHandClose = IsHandClose(palmPos);
            if (isHandClose && !_isHandClose)
            {
                HandProximityChanged?.Invoke(this, true);
            }
            else if(!isHandClose && _isHandClose)
            {
                HandProximityChanged?.Invoke(this, false);
            }

            _isHandClose = isHandClose;
        }
        else if (_isHandVisible)
        {
            _isHandVisible = false;
            HandVisibilityChanged?.Invoke(this, _isHandVisible);
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
        _logger.LogError("[LEAP]   PNP ID: {sn}", args.DeviceSerialNumber);
        _logger.LogError("[LEAP]   Failure message: {msg}", args.ErrorMessage);
    }

    private void OnLogMessage(object? sender, LogEventArgs args)
    {
        var (severity, level) = args.severity switch
        {
            MessageSeverity.MESSAGE_CRITICAL => ("Critical", LogLevel.Critical),
            MessageSeverity.MESSAGE_WARNING => ("Warning", LogLevel.Warning),
            MessageSeverity.MESSAGE_INFORMATION => ("Info", LogLevel.Information),
            _ => ("Unknown", LogLevel.Debug),
        };

        _logger.Log(level, "[LEAP] [{severity}] {type}: {msg}", severity, args.type, args.message);
    }

    #endregion
}
