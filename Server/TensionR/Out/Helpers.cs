namespace TensionR.API.Out;

internal static class ListExtension
{
    public static IList<byte> AddBlock(this IList<byte> list, int value)
    {
        byte v = (byte)value;

        list.Add(Packet.BlockStart);
        if (Packet.EscapingBytes.Contains(v))
        {
            list.Add(Packet.EscapeSymbol);
        }

        list.Add(v);

        return list;
    }
}
