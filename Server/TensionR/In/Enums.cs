namespace TensionR.API.In;

internal enum Motor
{
    Left = 0x31,
    Right = 0x32,
}

internal enum PacketPart : ushort
{
    First = 0x0000,
    Second = 0x0001, // however, only the Right motor may have this value, and it always arrives before the "first" part
}

internal enum PacketType : byte
{
    EchoFirst = 0x1b,
    EchoSecond = 0x2b,
    Status = 0x88,
}

internal enum OperationCode : byte
{
    StatusHight = 0xe8,
    StatusLow = 0xe9,
    StatusValue1 = 0xe2,
    StatusValue2 = 0xe4,
}

internal class ErrorCode
{
    public const byte NONE = 0x30;
    public const byte REQUEST_ERROR = 0xe4;
}
