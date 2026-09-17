namespace TensionR.API.Out;

internal class Packet : API.Packet
{
    // Block 0
    public Motor Motor;
    // Packet 1 is (Motor - 1)
    // Packet 2
    public PacketPart PartId = PacketPart.First;
    // Block 3
    public byte Code1 = OperationCode.Off;
    // Block 4-5
    public ushort Value1 = 0;
    // Packet 6
    public byte Code2 = OperationCode.Off;
    // Packet 7-8
    public ushort Value2 = 0;

    public byte[] ToBytes()
    {
        var result = new List<byte> { PacketOutStart }
            .AddBlock((int)Motor)
            .AddBlock((int)Motor - 1)
            .AddBlock((int)PartId)
            .AddBlock(Code1)
            .AddBlock((Value1 & 0xFF00) >> 8)
            .AddBlock(Value1 & 0x00FF)
            .AddBlock(Code2)
            .AddBlock((Value2 & 0xFF00) >> 8)
            .AddBlock(Value2 & 0x00FF);
        result.Add(PacketStop);
        return result.ToArray();
    }

    public static readonly Packet LeftOff =
        new(Motor.Left, PacketAndPartId.Off);
    public static readonly Packet LeftStart1 =
        new(Motor.Left, PacketAndPartId.Start1);
    public static readonly Packet LeftStart2_1 =
        new(Motor.Left, PacketAndPartId.Start2_1);
    public static readonly Packet LeftStart2_2 =
        new(Motor.Left, PacketAndPartId.Start2_2);
    public static readonly Packet LeftStart3_1 =
        new(Motor.Left, PacketAndPartId.Start3_1);
    public static readonly Packet LeftStart3_2 =
        new(Motor.Left, PacketAndPartId.Start3_2);
    public static readonly Packet LeftStop1 =
        new(Motor.Left, PacketAndPartId.Stop1);
    public static readonly Packet LeftStop2 =
        new(Motor.Left, PacketAndPartId.Stop2);
    public static readonly Packet LeftStop3_1 =
        new(Motor.Left, PacketAndPartId.Stop3_1);
    public static readonly Packet LeftStop3_2 =
        new(Motor.Left, PacketAndPartId.Stop3_2);

    public static readonly Packet RightOff =
        new(Motor.Right, PacketAndPartId.Off);
    public static readonly Packet RightStart1 =
        new(Motor.Right, PacketAndPartId.Start1);
    public static readonly Packet RightStart2_1 =
        new(Motor.Right, PacketAndPartId.Start2_1);
    public static readonly Packet RightStart2_2 =
        new(Motor.Right, PacketAndPartId.Start2_2);
    public static readonly Packet RightStart3_1 =
        new(Motor.Right, PacketAndPartId.Start3_1);
    public static readonly Packet RightStart3_2 =
        new(Motor.Right, PacketAndPartId.Start3_2);
    public static readonly Packet RightStop1 =
        new(Motor.Right, PacketAndPartId.Stop1);
    public static readonly Packet RightStop2 =
        new(Motor.Right, PacketAndPartId.Stop2);
    public static readonly Packet RightStop3_1 =
        new(Motor.Right, PacketAndPartId.Stop3_1);
    public static readonly Packet RightStop3_2 =
        new(Motor.Right, PacketAndPartId.Stop3_2);

    public static Packet LeftCalibration(int step) =>
        new(Motor.Left, PacketAndPartId.Calibration, step);
    public static Packet RightCalibration(int step) =>
        new(Motor.Right, PacketAndPartId.Calibration, step);

    public static Packet LeftTension(int step) =>
        new(Motor.Left, PacketAndPartId.Tension, step);
    public static Packet RightTension(int step) =>
        new(Motor.Right, PacketAndPartId.Tension, step);

    #region Internal

    enum PacketAndPartId
    {
        Off,
        Start1,
        Start2_1,
        Start2_2,
        Start3_1,
        Start3_2,
        Tension,
        Stop1,
        Stop2,
        Stop3_1,
        Stop3_2,
        Calibration
    }

    const ushort TENSION_MIN  = 0x3cb0;
    const ushort TENSION_STEP = 0x01f4;
    const ushort TENSION_MAX  = 0xc350;
    const ushort CALIBRATION_MIN  = 0x01e0;
    const ushort CALIBRATION_STEP = 0x0010;
    const ushort CALIBRATION_MAX  = 0xfe10;

    private Packet(Motor motor, PacketAndPartId id, int step = 0)
    {
        Motor = motor;

        if (id == PacketAndPartId.Off)
        {
            // already set
        }
        else if (id == PacketAndPartId.Tension)
        {
            Code1 = OperationCode.Tension_1;
            Code2 = OperationCode.Tension_2;
            if (Motor == Motor.Left)
            {
                Value1 = 0x0000;
                Value2 = (ushort)(0x10000 - StepToTension(step));
            }
            else if (Motor == Motor.Right)
            {
                Value1 = 0xffff;
                Value2 = StepToTension(step);
            }
        }
        else if (id == PacketAndPartId.Start1
              || id == PacketAndPartId.Stop1)
        {
            Code1 = OperationCode.StartStop_1_1;
            Code2 = OperationCode.StartStop_1_2;
        }
        else if (id == PacketAndPartId.Start2_1)
        {
            Code1 = OperationCode.Start_2_1_1;
            Code2 = OperationCode.Start_2_1_2;
            Value1 = 0x00d0;
            Value2 = 0x0505;
        }
        else if (id == PacketAndPartId.Start2_2)
        {
            //PacketId = PacketId.Second;  - Bug in TesnionR API? It seems to be logical to have PacketId = PacketId.Second for the second packet
            Code1 = OperationCode.Start_2_2_1;
            Code2 = OperationCode.Start_2_2_2;
            Value2 = 0x2000;
        }
        else if (id == PacketAndPartId.Stop2)
        {
            Code1 = OperationCode.Stop_2_X;
            Code2 = OperationCode.Stop_2_X;
            Value1 = 0x00c1;
            Value2 = 0x00c1;
        }
        else if (id == PacketAndPartId.Start3_1
              || id == PacketAndPartId.Stop3_1)
        {
            Code1 = OperationCode.StartStop_3_1_X;
            Code2 = OperationCode.StartStop_3_1_X;
            Value1 = 0x0001;
            Value2 = 0x0001;
        }
        else if (id == PacketAndPartId.Start3_2
              || id == PacketAndPartId.Stop3_2)
        {
            PartId = PacketPart.Second;
            Code1 = OperationCode.StartStop_3_2_1;
            Code2 = OperationCode.StartStop_3_2_2;
        }
        else if (id == PacketAndPartId.Calibration)
        {
            if (Motor == Motor.Left)
            {
                Value1 = Value2 = (ushort)(0x10000 - StepToCalibration(step));
            }
            else if (Motor == Motor.Right)
            {
                Value1 = Value2 = StepToCalibration(step);
            }
        }
    }

    private static ushort StepToTension(int step)
    {
        return (ushort)Math.Clamp(
            TENSION_MIN + step * TENSION_STEP,
            TENSION_MIN,
            TENSION_MAX);
    }

    private static ushort StepToCalibration(int step)
    {
        return (ushort)Math.Clamp(
            CALIBRATION_MIN + step * CALIBRATION_STEP,
            CALIBRATION_MIN,
            CALIBRATION_MAX);
    }

    #endregion
}

