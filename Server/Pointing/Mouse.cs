using SharpDX.DirectInput;
using Proto = global::Pointing;

namespace Server.Pointing;

class Mouse : PointingDevice
{
    public override DeviceType Type => DeviceType.Mouse;

    public Mouse() : base()
    {
        var mouse = new SharpDX.DirectInput.Mouse(_directInput);
        mouse.Properties.BufferSize = 128;
        mouse.Acquire();

        _mouse = mouse;
    }

    #region

    readonly SharpDX.DirectInput.Mouse _mouse;

    protected override void Step()
    {
        if (_mouse == null)
            return;

        _mouse.Poll();
        var datas = _mouse.GetBufferedData();
        if (datas.Length > 0)
        {
            foreach (var data in datas)
            {
                if (data.Offset == MouseOffset.X)
                    _x = data.Value;
                else if (data.Offset == MouseOffset.Y)
                    _y = data.Value;
                else if (data.Offset == MouseOffset.Z)
                    _z = data.Value;
                else
                    lock (_buttons)
                    {
                        _buttons.Add(new Proto.Button()
                        {
                            Offset = data.RawOffset,
                            Id = (int)data.Offset - (int)MouseOffset.Buttons0,
                            Name = Proto.ControlIds.Button,
                            IsPressed = data.Value > 0,
                        });
                    }
            }
        }
    }

    #endregion
}
