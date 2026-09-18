using SharpDX.DirectInput;
using Proto = global::Pointing;

namespace Server.Pointing;

class Joystick : Controller
{
    public enum NameComparisionOption
    {
        Equals,
        StartsWith,
        Contains
    }

    public override DeviceType Type { get; }
    public override bool IsCreated => _joystick != null;

    public Joystick() : base() { }

    public Joystick(string name,
        DeviceType deviceType = DeviceType.Joystick,
        NameComparisionOption comparisionOption = NameComparisionOption.Equals)
        : base()
    {
        if (string.IsNullOrEmpty(name))
            return;

        Type = deviceType;
        var devices = ListDevices(deviceType);
        Create(devices, name, comparisionOption);
    }

    #region Internal

    protected SharpDX.DirectInput.Joystick? _joystick;

    protected override void Step()
    {
        if (_joystick == null)
            return;

        _joystick.Poll();

        var datas = _joystick.GetBufferedData();

        if (datas.Length > 0)
        {
            foreach (var data in datas)
            {
                if (data.Offset == JoystickOffset.X)
                    _x = (double)(data.Value - 0x8000) / 0x8000;
                else if (data.Offset == JoystickOffset.Y)
                    _y = (double)(data.Value - 0x8000) / 0x8000;
                else if (data.Offset == JoystickOffset.Z)
                    _z = (double)(data.Value - 0x8000) / 0x8000;
                else
                {
                    var name = data.Offset.ToString();
                    if (name.StartsWith("Button"))
                    {
                        lock (_buttons)
                        {
                            _buttons.Add(new Proto.Button()
                            {
                                Offset = data.RawOffset,
                                Id = (int)data.Offset - (int)JoystickOffset.Buttons0,
                                Name = Proto.ControlIds.Button,
                                IsPressed = data.Value > 0,
                            });
                        }
                    }
                    else if (name.StartsWith("PointOfView"))
                    {
                        lock (_povs)
                        {
                            _povs.Add(new Proto.PointOfView()
                            {
                                Offset = data.RawOffset,
                                Id = int.Parse(name[^1..]),
                                Name = Proto.ControlIds.PointOfView,
                                IsPressed = data.Value >= 0,
                                Degrees = data.Value >= 0 ? (double)data.Value / 100 : 0,
                            });
                        }
                    }
                    else if (name.Contains("Slider"))
                    {
                        lock (_sliders)
                        {
                            _sliders.Add(new Proto.Slider()
                            {
                                Offset = data.RawOffset,
                                Id = int.Parse(name[^1..]),
                                Name = Proto.ControlIds.Slider,
                                Value = (double)data.Value / 0xFFFF,
                                Type = data.Offset switch
                                {
                                    JoystickOffset.Sliders0 or JoystickOffset.Sliders1 =>
                                        Proto.SliderType.General,
                                    JoystickOffset.VelocitySliders0 or JoystickOffset.VelocitySliders1 =>
                                        Proto.SliderType.Velocity,
                                    JoystickOffset.AccelerationSliders0 or JoystickOffset.AccelerationSliders1 =>
                                        Proto.SliderType.Acceleration,
                                    JoystickOffset.ForceSliders0 or JoystickOffset.ForceSliders1 =>
                                        Proto.SliderType.Force,
                                    _ => throw new NotImplementedException()
                                }
                            });
                        }
                    }
                    else if (name.StartsWith("Rotation"))
                    {
                        if (data.Offset == JoystickOffset.RotationX)
                            _rotation.X = (double)(data.Value - 0x8000) / 0x8000;
                        if (data.Offset == JoystickOffset.RotationY)
                            _rotation.Y = (double)(data.Value - 0x8000) / 0x8000;
                        if (data.Offset == JoystickOffset.RotationZ)
                            _rotation.Z = (double)(data.Value - 0x8000) / 0x8000;
                    }
                    else
                    {
                        Console.WriteLine($"data from {name} unhandled");
                    }
                    /* JOYSTICK_DATA
                    else if (name.StartsWith("Velocity"))
                    {
                        if (data.Offset == JoystickOffset.VelocityX)
                            velocity.X = data.Value;
                        if (data.Offset == JoystickOffset.VelocityY)
                            velocity.Y = data.Value;
                        if (data.Offset == JoystickOffset.VelocityZ)
                            velocity.Z = data.Value;
                    }
                    else if (name.StartsWith("AngularVelocity"))
                    {
                        if (data.Offset == JoystickOffset.AngularVelocityX)
                            angularVelocity.X = data.Value;
                        if (data.Offset == JoystickOffset.AngularVelocityY)
                            angularVelocity.Y = data.Value;
                        if (data.Offset == JoystickOffset.AngularVelocityZ)
                            angularVelocity.Z = data.Value;
                    }
                    else if (name.StartsWith("Acceleration"))
                    {
                        if (data.Offset == JoystickOffset.AccelerationX)
                            acceleration.X = data.Value;
                        if (data.Offset == JoystickOffset.AccelerationY)
                            acceleration.Y = data.Value;
                        if (data.Offset == JoystickOffset.AccelerationZ)
                            acceleration.Z = data.Value;
                    }
                    else if (name.StartsWith("AngularAcceleration"))
                    {
                        if (data.Offset == JoystickOffset.AngularAccelerationX)
                            angularAcceleration.X = data.Value;
                        if (data.Offset == JoystickOffset.AngularAccelerationY)
                            angularAcceleration.Y = data.Value;
                        if (data.Offset == JoystickOffset.AngularAccelerationZ)
                            angularAcceleration.Z = data.Value;
                    }
                    else if (name.StartsWith("Force"))
                    {
                        if (data.Offset == JoystickOffset.ForceX)
                            force.X = data.Value;
                        if (data.Offset == JoystickOffset.ForceY)
                            force.Y = data.Value;
                        if (data.Offset == JoystickOffset.ForceZ)
                            force.Z = data.Value;
                    }
                    else if (name.StartsWith("Torque"))
                    {
                        if (data.Offset == JoystickOffset.TorqueX)
                            torque.X = data.Value;
                        if (data.Offset == JoystickOffset.TorqueY)
                            torque.Y = data.Value;
                        if (data.Offset == JoystickOffset.TorqueZ)
                            torque.Z = data.Value;
                    }*/
                }
            }
        }
    }

    protected override void Close()
    {
        _joystick?.Unacquire();
    }

    protected void Create(
        DeviceInstance[] devices,
        string name,
        NameComparisionOption comparisionOption)
    {
        var selectedDevice = devices.FirstOrDefault(device => comparisionOption switch
        {
            NameComparisionOption.Equals =>
                device.ProductName.Equals(name, StringComparison.OrdinalIgnoreCase),
            NameComparisionOption.StartsWith =>
                device.ProductName.StartsWith(name, StringComparison.OrdinalIgnoreCase),
            NameComparisionOption.Contains =>
                device.ProductName.Contains(name, StringComparison.OrdinalIgnoreCase),
            _ => throw new NotImplementedException()
        });

        if (selectedDevice != null)
        {
            var joystick = new SharpDX.DirectInput.Joystick(
                _directInput,
                selectedDevice.InstanceGuid
            );
            joystick.Properties.BufferSize = 128;
            joystick.Acquire();

            _joystick = joystick;
        }
    }

    #endregion
}
