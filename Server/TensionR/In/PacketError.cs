namespace TensionR.API.In;

internal class PacketError : Packet
{
    public byte ErrorCode { get; init; }
    public int Count { get; init; }

    public new byte[] ToBytes()
    {
        return [ErrorCode, ..Values[..Count]];
    }

    public override string ToString()
    {
        return string.Join(' ',
            Values
                .Take(Count)
                .Select(x => $"{x:X2}")
        );
    }

    public static new PacketError FromContent(byte[] input)
    {
        List<byte> values = [];
        int packetId = -1;

        for (int i = 0; i < input.Length; i++)
        {
            byte b = input[i];
            if (b == BlockStart)
            {
                packetId++;
            }
            else
            {
                if (b == Packet.EscapeSymbol)
                {
                    b = input[++i];
                }

                values.Add(b);
            }
        }

        var result = new PacketError()
        {
            ErrorCode = values[0],
            Count = values.Count - 1,
        };

        for (int i = 1; i < Math.Min(values.Count, result.Values.Length + 1); i++)
            result.Values[i - 1] = values[i];

        return result;
    }
}
