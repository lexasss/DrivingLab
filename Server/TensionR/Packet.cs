namespace TensionR.API;

internal class Packet
{
    public const byte Zero = 0x00;
    public const byte PacketOutStart = 0x32;
    public const byte PacketInStart = 0x33;
    public const byte BlockStart = 0x2c;
    public const byte PacketStop = 0x3b;

    public const byte EscapeSymbol = 0x2f;

    public static byte[] EscapingBytes = [
        Zero,
        PacketOutStart,
        PacketInStart,
        BlockStart,
        PacketStop
    ];
}
