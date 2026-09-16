using SharpDX.DirectInput;

namespace Server.Pointing;

class Gamepad : Joystick
{
    public override DeviceType Type => DeviceType.Gamepad;

    public Gamepad(string name) : base()
    {
        var devices = ListDevices(DeviceType.Gamepad);
        Create(devices, name);
    }
}
