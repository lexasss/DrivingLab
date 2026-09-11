namespace TensionR.API.Out;

internal enum Motor : byte
{
    Left = 0x01,
    Right = 0x02,
}

internal enum PacketPart : byte
{
    First = 0x1a,
    Second = 0x2a,
}
