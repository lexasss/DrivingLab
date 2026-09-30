using System.Runtime.InteropServices;
using Proto = global::Driving;

namespace Server.Driving;

internal partial class SimucubeApi
{
    // Enums moved to Proto

    private const string DllName = "sc-api-effects.dll";

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static partial Proto.SimucubeDevice Init(long timeoutInSeconds);

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static partial void Configure(
        Proto.SimucubeDevice pedal,
        Proto.EffectVariable variable);

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static partial void ConfigurePeriodic(
        Proto.PeriodicEffectType type,
        float frequency);

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static partial void Run(
        Proto.SimucubeDevice pedal,
        Proto.EffectType effectType,
        int durationMs,
        float amplitude);

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static partial void Stop(Proto.SimucubeDevice pedal);
}
