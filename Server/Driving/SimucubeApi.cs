using System.Runtime.InteropServices;

namespace Server.Driving;

internal partial class SimucubeApi
{
    [Flags]
    public enum Pedal
    {
        None = 0,
        Brake = 1,
        Throttle = 2,
        Both = Brake | Throttle
    }

    public enum EffectType
    {
        Constant = 0,
        Periodic = 1
    }

    public enum OffsetType
    {
        TorqueNm = 0,
        TorqueRelative = 1,
        ForceN = 2,
        ForceRelative = 3,
        PositionMm = 4
    }

    private const string DllName = "sc-api-effects.dll";

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static partial Pedal Init(long timeoutInSeconds);

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static partial void Configure(
        Pedal pedal,
        OffsetType offsetType);

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static partial void Run(
        Pedal pedal,
        EffectType effectType,
        int durationMs,
        float amplitude);
}
