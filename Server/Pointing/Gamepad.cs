using SharpDX.DirectInput;

namespace Server.Pointing;

class Gamepad : Joystick
{
    public override DeviceType Type => DeviceType.Gamepad;

    public Gamepad(string name) : base(string.Empty)
    {
        var devices = ListDevices(DeviceType.Gamepad);
        var selectedDevice = devices.FirstOrDefault(device => device.ProductName.Equals(name)) ?? devices.FirstOrDefault();

        if (selectedDevice != null)
        {
            var joystick = new SharpDX.DirectInput.Joystick(_directInput, selectedDevice.InstanceGuid);
            joystick.Properties.BufferSize = 128;
            joystick.Acquire();

            _joystick = joystick;
        }
    }
}
