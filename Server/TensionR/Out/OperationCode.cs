namespace TensionR.API.Out;

/// <summary>
/// Values of blocks 3 and 6
/// </summary>
internal static class OperationCode
{
    // naming semantics: name[_packetId[_partId]]_blockId
    //      name:     can be combined (like "StartStop") operation name if these operation share the code
    //      packetId: Start and Stop operations consists of 3 packets, so it can be 1, 2 and 3
    //      partId:   1 = single, or first part in the two-part packet, 2 = second part in the two-part packet
    //                Start 2nd and 3rd and Stop 3rd packets consist of two parts.
    //                Note that the 2nd part in Start 2nd packet has, weirdly, "1" (probably, a bug in API)
    //      blockId:  1 = Block 3, 2 = Block 6, X - any of these
    public const byte Off = 0x08;
    public const byte Tension_1 = 0x50;
    public const byte Tension_2 = 0x05;
    public const byte StartStop_1_1 = 0x4a;
    public const byte StartStop_1_2 = 0x4c;
    public const byte Start_2_1_1 = 0x02;
    public const byte Start_2_1_2 = 0x09;
    public const byte Start_2_2_1 = 0x51;
    public const byte Start_2_2_2 = 0x1d;
    public const byte Stop_2_X = 0x02;
    public const byte StartStop_3_1_X = 0x00;
    public const byte StartStop_3_2_1 = 0xe1;
    public const byte StartStop_3_2_2 = 0xe3;
}
