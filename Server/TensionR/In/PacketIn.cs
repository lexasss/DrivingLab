namespace TensionR.API.In;

internal class Packet : API.Packet
{
    // Block 0
    public Motor Motor { get; init; }
    // Blocks 1-9
    public byte[] Values { get; init; } = new byte[9];

    // Interpreted fields

    public PacketPart PacketPart => (PacketPart)((Values[0] << 8) | Values[1]);
    public PacketType PacketType => (PacketType)Values[2];
    public byte Code1 => Values[3];
    public ushort Value1 => (ushort)((Values[4] << 8) | Values[5]);
    public byte Code2 => Values[6];
    public ushort Value2 => (ushort)((Values[7] << 8) | Values[8]);
    public ushort Value => (ushort)((Value1 << 16) | Value2);

    public bool IsEcho => PacketType == PacketType.EchoFirst
                       || PacketType == PacketType.EchoSecond;

    /// <param name="input">Raw data received from port</param>
    /// <returns>List of packets</returns>
    public static Packet[] FromRaw(byte[] input)
    {
        var packets = new List<Packet>();

        int startIndex = 0;
        bool isEscaped = false;

        for (int i = 0; i < input.Length; i++)
        {
            var v = input[i];

            if (v == Packet.PacketInStart && !isEscaped)
            {
                startIndex = i + 1;
            }
            else if (v == Packet.PacketStop && !isEscaped)
            {
                var packet = FromContent(input[startIndex..i]);
                if (packet != null)
                    packets.Add(packet);
            }
            else if (v == Packet.EscapeSymbol)
            {
                isEscaped = true;
            }
            else if (isEscaped)
            {
                isEscaped = false;
            }
        }

        return packets.ToArray();
    }

    /// <param name="input">bytes recevied from port, between
    /// <see cref="TensionR.Packet.PacketInStart"/> and 
    /// <see cref="TensionR.Packet.PacketStop"/></param>
    /// <returns>new packet, if created</returns>
    public static Packet? FromContent(byte[] input)
    {
        byte errorCode = ErrorCode.NONE;
        Motor motor = Motor.Left;
        byte[] values = new byte[9];

        int packetId = -1;

        for (int i = 0; i < input.Length; i++)
        {
            byte b = input[i];
            if (b == BlockStart)
            {
                packetId++;
            }
            else if (packetId == 0)
            {
                if (b == ErrorCode.NONE)
                {
                    var b2 = input[++i];
                    if (b2 == ErrorCode.NONE)
                    {
                        var b3 = input[++i];
                        if (b3 != (byte)Motor.Left && b3 != (byte)Motor.Right)
                            return null;
                        else
                            motor = (Motor)b3;
                    }
                    else
                    {
                        errorCode = b2;
                    }
                }
                else
                {
                    errorCode = b;
                }

                if (errorCode != ErrorCode.NONE)
                {
                    return PacketError.FromContent(input);
                }
            }
            else
            {
                if (b == Packet.EscapeSymbol)
                {
                    b = input[++i];
                }

                values[packetId - 1] = b;
            }
        }

        var result = new Packet()
        {
            Motor = motor,
        };

        for (int i = 0; i < values.Length; i++)
            values.CopyTo(result.Values.AsSpan());

        return result;
    }

    public override string ToString()
    {
        List<string> fields = [$"{Motor.ToString()[0]}"];
        for (int i = 0; i < Values.Length; i++)
        {
            fields.Add($"{Values[i]:X2}");
        }
        return string.Join(' ', fields);
    }

    public byte[] ToBytes()
    {
        return [(byte)Motor, ..Values];
    }
}

