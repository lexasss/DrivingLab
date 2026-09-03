using Google.Protobuf.WellKnownTypes;
using Grpc.Core;

namespace Server;

internal interface IService : IDisposable
{
    bool IsAvailable();
    Task<Common.Bool> IsAvailable(Empty request, ServerCallContext context);
}

internal interface ITelemetryService : IService
{
    Task<Empty> Start(Empty request, ServerCallContext context);
    Task<Empty> Stop(Empty request, ServerCallContext context);
    Task<Common.Bool> SetLogFileName(Common.String request, ServerCallContext context);
}
