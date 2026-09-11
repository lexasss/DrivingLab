namespace TensionR.API;

[Flags]
internal enum Side
{
    Left = 0x01,
    Right = 0x02,
    Both = Left | Right,
}

internal class Communicator : IDisposable
{
    public enum State
    {
        Off,
        Starting,
        On,
        CalibrationStart,
        Calibrating,
        CalibrationStop,
        Stopping,
    }

    public class RequestEventArgs(State state, Out.Packet[] packets) : EventArgs
    {
        public State State { get; } = state;
        public Out.Packet[] Packets { get; } = packets;
    }

    public event EventHandler<RequestEventArgs>? Request;

    public Communicator()
    {
        Task.Run(async () =>
        {
            while (!_isDisposed)
            {
                await Task.Delay(INTERVAL);
                Out.Packet[] packets = _state switch
                {
                    State.Off => 
                          GetPackets(BeltControl.Off, Side.Both),
                    State.Starting => [
                        ..GetPackets(BeltControl.Start, Side.Both, 0),
                        ..GetPackets(BeltControl.Start, Side.Both, 1),
                        ..GetPackets(BeltControl.Start, Side.Both, 2),
                    ],
                    State.On => [
                        ..GetPackets(BeltControl.Tension, Side.Left, _leftTension),
                        ..GetPackets(BeltControl.Tension, Side.Right, _rightTension),
                    ],
                    State.Stopping => [
                        ..GetPackets(BeltControl.Stop, Side.Both, 0),
                        ..GetPackets(BeltControl.Stop, Side.Both, 1),
                        ..GetPackets(BeltControl.Stop, Side.Both, 2),
                    ],
                    State.CalibrationStart => [
                        ..GetPackets(BeltControl.Stop, Side.Both, 0),
                        ..GetPackets(BeltControl.Stop, Side.Both, 1),
                        ..GetPackets(BeltControl.Stop, Side.Both, 2),
                    ],
                    State.Calibrating => 
                          GetPackets(BeltControl.Calibrate, Side.Both, _calibrationIndex++),
                    State.CalibrationStop => [
                        ..GetPackets(BeltControl.Off, Side.Both),
                        ..GetPackets(BeltControl.Start, Side.Both, 0),
                        ..GetPackets(BeltControl.Start, Side.Both, 1),
                        ..GetPackets(BeltControl.Start, Side.Both, 2),
                    ],
                    _ => throw new InvalidOperationException("Unknown state")
                };

                Request?.Invoke(this, new RequestEventArgs(_state, packets));

                if (_state == State.Starting)
                    _state = State.On;
                else if (_state == State.Stopping)
                    _state = State.Off;
                else if (_state == State.CalibrationStart)
                    _state = State.Calibrating;
                else if (_state == State.CalibrationStop)
                {
                    _state = State.On;
                    _isCalibrated = true;
                }
                else if (_state == State.Calibrating)
                {
                    if (_calibrationIndex == CALIBRATION_CYCLE_COUNT)
                    {
                        _state = State.CalibrationStop;
                        _calibrationIndex = 0;
                    }
                }
            }
        });
    }

    public void Dispose()
    {
        _isDisposed = true;
        GC.SuppressFinalize(this);
    }

    public void Start()
    {
        if (_state == State.Off)
            _state = State.Starting;
    }

    public void Stop()
    {
        if (_state == State.On)
            _state = State.Stopping;
    }

    public void Calibrate()
    {
        if (_state == State.On && !_isCalibrated)
            _state = State.CalibrationStart;
    }

    public void IncreaseTension(Side side = Side.Both)
    {
        if (_state == State.On && _isCalibrated)
        {
            if (side.HasFlag(Side.Left))
                _leftTension = Math.Clamp(_leftTension + 1, TENSION_MIN, TENSION_MAX);
            if (side.HasFlag(Side.Right))
                _rightTension = Math.Clamp(_rightTension + 1, TENSION_MIN, TENSION_MAX);
        }
    }

    public void DecreaseTension(Side side = Side.Both)
    {
        if (_state == State.On && _isCalibrated)
        {
            if (side.HasFlag(Side.Left))
                _leftTension = Math.Clamp(_leftTension - 1, TENSION_MIN, TENSION_MAX);
            if (side.HasFlag(Side.Right))
                _rightTension = Math.Clamp(_rightTension - 1, TENSION_MIN, TENSION_MAX);
        }
    }

    public void SetTension(int value, Side side = Side.Both)
    {
        if (_state == State.On && _isCalibrated)
        {
            if (side.HasFlag(Side.Left))
                _leftTension = Math.Clamp(value, TENSION_MIN, TENSION_MAX);
            if (side.HasFlag(Side.Right))
                _rightTension = Math.Clamp(value, TENSION_MIN, TENSION_MAX);
        }
    }

    #region Internal

    enum BeltControl
    {
        Off,
        Start,
        Stop,
        Calibrate,
        Tension
    }

    const int INTERVAL = 150;    // ms
    const int TENSION_MIN = 0;
    const int TENSION_MAX = 64;
    const int CALIBRATION_CYCLE_COUNT = 25;

    State _state = State.Off;
    bool _isDisposed = false;
    int _calibrationIndex = 0;
    bool _isCalibrated = false;

    int _leftTension = 0;
    int _rightTension = 0;

    private static Out.Packet[] GetPackets(BeltControl control, Side side = Side.Both, int step = 0)
    {
        if (control == BeltControl.Off)
            return side switch
            {
                Side.Left => [Out.Packet.LeftOff],
                Side.Right => [Out.Packet.RightOff],
                _ => [Out.Packet.LeftOff, Out.Packet.RightOff]
            };
        else if (control == BeltControl.Start)
            return side switch
            {
                Side.Left => step switch
                {
                    0 => [Out.Packet.LeftStart1],
                    1 => [Out.Packet.LeftStart2_1, Out.Packet.LeftStart2_2],
                    2 => [Out.Packet.LeftStart3_1, Out.Packet.LeftStart3_2],
                    _ => []
                },
                Side.Right => step switch
                {
                    0 => [Out.Packet.RightStart1],
                    1 => [Out.Packet.RightStart2_1, Out.Packet.RightStart2_2],
                    2 => [Out.Packet.RightStart3_1, Out.Packet.RightStart3_2],
                    _ => [],
                },
                _ => step switch
                {
                    0 => [Out.Packet.LeftStart1, Out.Packet.RightStart1],
                    1 => [
                        Out.Packet.LeftStart2_1,
                        Out.Packet.LeftStart2_2,
                        Out.Packet.RightStart2_1,
                        Out.Packet.RightStart2_2,
                    ],
                    2 => [
                        Out.Packet.LeftStart3_1,
                        Out.Packet.LeftStart3_2,
                        Out.Packet.RightStart3_1,
                        Out.Packet.RightStart3_2,
                    ],
                    _ => []
                }
            };
        else if (control == BeltControl.Stop)
            return side switch
            {
                Side.Left => step switch
                {
                    0 => [Out.Packet.LeftStop1],
                    1 => [Out.Packet.LeftStop2],
                    2 => [Out.Packet.LeftStop3_1, Out.Packet.LeftStop3_2],
                    _ => []
                },
                Side.Right => step switch
                {
                    0 => [Out.Packet.RightStop1],
                    1 => [Out.Packet.RightStop2],
                    2 => [Out.Packet.RightStop3_1, Out.Packet.RightStop3_2],
                    _ => []
                },
                _ => step switch
                {
                    0 => [Out.Packet.LeftStop1, Out.Packet.RightStop1],
                    1 => [Out.Packet.LeftStop2, Out.Packet.RightStop2],
                    2 => [
                        Out.Packet.LeftStop3_1,
                        Out.Packet.LeftStop3_2,
                        Out.Packet.RightStop3_1,
                        Out.Packet.RightStop3_2,
                    ],
                    _ => []
                }
            };
        else if (control == BeltControl.Calibrate)
            return side switch
            {
                Side.Left => [Out.Packet.LeftCalibration(step)],
                Side.Right => [Out.Packet.RightCalibration(step)],
                _ => [
                    Out.Packet.LeftCalibration(step),
                    Out.Packet.RightCalibration(step)
                ]
            };
        else if (control == BeltControl.Tension)
        {
            return side switch
            {
                Side.Left => [Out.Packet.LeftTension(step)],
                Side.Right => [Out.Packet.RightTension(step)],
                _ => [
                    Out.Packet.LeftTension(step),
                    Out.Packet.RightTension(step)
                ]
            };
        }

        return [];
    }

    #endregion
}
