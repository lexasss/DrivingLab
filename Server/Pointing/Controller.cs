using SharpDX.DirectInput;
using Proto = global::Pointing;

namespace Server.Pointing;

abstract class Controller : IDisposable
{
    public abstract DeviceType Type { get; }
    public abstract bool IsCreated { get; }

    public event EventHandler<Proto.Data>? Data;
    public event EventHandler? Disconnected;

    public Controller()
    {
        _timer.Interval = SAMPLING_INTERVAL;
        _timer.AutoReset = true;
        _timer.Elapsed += Timer_Elapsed;
        _timer.Start();

        Task.Run(async() => RunCycle(_cts.Token));
    }

    public void Dispose()
    {
        _cts.Cancel();
        _timer.Dispose();

        Close();
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

    public static DeviceInstance[] ListDevices(params DeviceType[] types)
    {
        List<DeviceInstance> devices = [];

        foreach (var type in types)
        {
            var attachedDevices = _directInput.GetDevices(type, DeviceEnumerationFlags.AttachedOnly);
            foreach (var deviceInstance in attachedDevices)
            {
                devices.Add(deviceInstance);
            }
        }

        return devices.ToArray();
    }

    public static bool Has(DeviceType type) =>
        _directInput
            .GetDevices(type, DeviceEnumerationFlags.AttachedOnly)
            .Count > 0;

    // Internal

    const int POLLING_INTERVAL = 10;
    const int SAMPLING_INTERVAL = 20;

    readonly System.Timers.Timer _timer = new();
    readonly CancellationTokenSource _cts = new();

    protected static readonly DirectInput _directInput = new();

    protected double _x = 0;
    protected double _y = 0;
    protected double _z = 0;
    protected List<Proto.Button> _buttons = [];
    protected List<Proto.Slider> _sliders = [];
    protected List<Proto.PointOfView> _povs = [];
    protected Common.Vector _rotation = Common.Vector.Empty;
    protected Common.Vector _velocity = Common.Vector.Empty;
    protected Common.Vector _angularVelocity = Common.Vector.Empty;
    protected Common.Vector _acceleration = Common.Vector.Empty;
    protected Common.Vector _angularAcceleration = Common.Vector.Empty;
    protected Common.Vector _force = Common.Vector.Empty;
    protected Common.Vector _torque = Common.Vector.Empty;

    protected abstract void Step(); // this should update _x, _y and _buttons
    protected abstract void Close(); // this should call Unaquire()

    protected void OnDisconnected()
    {
        Disconnected?.Invoke(this, EventArgs.Empty);
    }

    private async void RunCycle(CancellationToken cts)
    {
        await Task.Delay(100);    // just in case, as this loop may start earlier then a device is initialized

        while (!cts.IsCancellationRequested)
        {
            try
            {
                Step();
            }
            catch
            {
                _timer.Stop();
                OnDisconnected();
                break;
            }

            Thread.Sleep(POLLING_INTERVAL);
        }
    }

    private void Timer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        var data = new Proto.Data
        {
            Point = new Common.Vector() {
                X = _x, Y = _y, Z = _z
            },
            Rotation = _rotation,
            /* JOYSTICK_DATA
            Velocity = _velocity,
            AngularVelocity = _angularVelocity,
            Acceleration = _acceleration,
            AngularAcceleration = _angularAcceleration,
            Force = _force,
            Torque = _torque
            */
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

        Data?.Invoke(this, data);
    }
}