using SharpDX.DirectInput;
using Proto = global::Pointing;

namespace Server.Pointing;

abstract class PointingDevice : IDisposable
{
    public abstract DeviceType Type { get; }

    public event EventHandler<Proto.Data>? Updated;

    public PointingDevice()
    {
        _timer.Interval = 20;
        _timer.AutoReset = true;
        _timer.Elapsed += Timer_Elapsed;
        _timer.Start();

        Task.Run(async() => RunCycle(_cts.Token));
    }

    public void Dispose()
    {
        _cts.Cancel();
        _timer.Dispose();
    }

    public void Reset()
    {
        lock (_buttons)
            _buttons.Clear();
        lock (_sliders)
            _sliders.Clear();
        lock (_povs)
            _povs.Clear();
    }

    public static DeviceInstance[] ListDevices(DeviceType type)
    {
        List<DeviceInstance> devices = [];

        foreach (var deviceInstance in _directInput.GetDevices(type, DeviceEnumerationFlags.AttachedOnly))
        {
            devices.Add(deviceInstance);
        }

        return devices.ToArray();
    }

    public static bool Has(DeviceType type)
    {
        return _directInput.GetDevices(type, DeviceEnumerationFlags.AllDevices).Count > 0;
    }

    public static DeviceType GetFirstExistingType()
    {
        if (Has(DeviceType.Mouse))
            return DeviceType.Mouse;
        else if (Has(DeviceType.Joystick))
            return DeviceType.Joystick;
        else if (Has(DeviceType.Gamepad))
            return DeviceType.Gamepad;

        throw new InvalidProgramException();
    }

    // Internal

    readonly System.Timers.Timer _timer = new();
    readonly CancellationTokenSource _cts = new();

    protected static readonly DirectInput _directInput = new();

    protected double _x = 0;
    protected double _y = 0;
    protected double _z = 0;
    protected List<Proto.Button> _buttons = [];
    protected List<Proto.Slider> _sliders = [];
    protected List<Proto.PointOfView> _povs = [];
    protected Common.Vector rotation = Common.Vector.Empty;
    protected Common.Vector velocity = Common.Vector.Empty;
    protected Common.Vector angularVelocity = Common.Vector.Empty;
    protected Common.Vector acceleration = Common.Vector.Empty;
    protected Common.Vector angularAcceleration = Common.Vector.Empty;
    protected Common.Vector force = Common.Vector.Empty;
    protected Common.Vector torque = Common.Vector.Empty;

    protected abstract void Step(); // this should update _x, _y and _buttons

    private async void RunCycle(CancellationToken cts)
    {
        await Task.Delay(100, cts);    // just in case, as this loop may start earlier then a device is initialized

        while (!cts.IsCancellationRequested)
        {
            Step();
            Thread.Sleep(10);
        }
    }

    private void Timer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        var data = new Proto.Data
        {
            Point = new Common.Vector() { X = _x, Y = _y, Z = _z },
            Rotation = rotation,
            Velocity = velocity,
            AngularVelocity = angularVelocity,
            Acceleration = acceleration,
            AngularAcceleration = angularAcceleration,
            Force = force,
            Torque = torque
        };

        lock (_buttons)
        {
            foreach (var button in _buttons)
            {
                data.Buttons.Add(button);
            }

            _buttons.Clear();
        }

        lock (_sliders)
        {
            foreach (var slider in _sliders)
            {
                data.Sliders.Add(slider);
            }

            _sliders.Clear();
        }

        lock (_povs)
        {
            foreach (var pov in _povs)
            {
                data.PointOfViews.Add(pov);
            }

            _povs.Clear();
        }

        Updated?.Invoke(this, data);
    }
}