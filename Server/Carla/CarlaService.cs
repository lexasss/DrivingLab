using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;
using Proto = global::Carla;

namespace Server.Carla;

internal class CarlaService : 
    Proto.Dispatcher.DispatcherBase,
    ITelemetryService
{
    public bool IsAvailable() => true;

    public CarlaService(ILoggerFactory loggerFactory) : base()
    {
        _logger = loggerFactory.CreateLogger("CARL");
        _logger.LogInformation("Running");

        _baseService = new(_logger);

        try
        {
            _udp = new UdpClient(new IPEndPoint(IPAddress.Parse(IpLocal), UdpPort));
            _logger.LogError("Listening on {ip}:{port}", IpLocal, UdpPort);

            _ = RunListenerLoop(_udp);
        }
        catch (Exception ex)
        {
            _logger.LogError("Cannot start the service ({ex})", ex.Message);
        }
    }

    public void Dispose()
    {
        _udp?.Dispose();
        _baseService?.Dispose();

        GC.SuppressFinalize(this);
    }

    public override Task<Common.Bool> IsAvailable(
        Empty request,
        ServerCallContext context)
    {
        return Common.Awaitable.From(IsAvailable());
    }

    public override Task<Common.Bool> IsStreamingData(
        Empty request,
        ServerCallContext context)
    {
        return Common.Awaitable.From(_baseService?.IsSending == true);
    }

    public override Task<Common.Bool> IsLogging(
        Empty request,
        ServerCallContext context)
    {
        return Common.Awaitable.From(_baseService?.IsLogging == true);
    }

    public override async Task ReadData(
        Empty request,
        IServerStreamWriter<Proto.Data> responseStream,
        ServerCallContext context)
    {
        if (_baseService == null)
            return;

        await _baseService.ReadData(request, responseStream, context, 3);
    }

    public override async Task ReadEvents(
        Empty request,
        IServerStreamWriter<Proto.Event> responseStream,
        ServerCallContext context)
    {
        if (_baseService == null)
            return;

        await _baseService.ReadEvents(request, responseStream, context);
    }


    public override Task<Empty> Start(
        Empty request,
        ServerCallContext context)
    {
        if (_baseService?.IsSending == false)
        {
            _baseService.Start();
        }
        return Common.Awaitable.Empty;
    }

    public override Task<Empty> Stop(
        Empty request,
        ServerCallContext context)
    {
        _baseService?.Stop();
        return Common.Awaitable.Empty;
    }

    public override Task<Common.Bool> SetLogFileName(
        Common.String request,
        ServerCallContext context)
    {
        if (_baseService == null)
            return Common.Awaitable.False;

        return _baseService.SetLogFileName(request.Value);
    }

    public override Task<Common.Strings> GetTelemetryFields(
        Empty request,
        ServerCallContext context)
    {
        return Common.Awaitable.From(Telemetry.Fields);
    }

    public override Task<Common.Bool> SetStreamFields(
        Common.Strings fields,
        ServerCallContext context)
    {
        bool allFieldsAreValid = true;
        _streamingFields.Clear();
        foreach (var field in fields.Items)
        {
            if (Telemetry.Fields.Contains(field))
                _streamingFields.Add(field);
            else
                allFieldsAreValid = false;
        }
        return Common.Awaitable.From(allFieldsAreValid);
    }

    public override Task<Common.Strings> GetStreamFields(
        Empty request,
        ServerCallContext context)
    {
        return Common.Awaitable.From(_streamingFields);
    }

    #region Internal

    const string IpLocal = "127.0.0.1";
    const int UdpPort = 20777;

    readonly ILogger _logger;
    readonly Tools.TelemetryService<Proto.Data, Proto.Event>? _baseService;
    readonly UdpClient? _udp;

    readonly List<string> _streamingFields = [];

    private async Task RunListenerLoop(UdpClient udp)
    {
        var ip = IPEndPoint.Parse("127.0.0.1");
        try
        {
            while (true)
            {
                //UdpReceiveResult result = await udp.ReceiveAsync();
                await Task.Delay(20);
                var result = new UdpReceiveResult(Telemetry.Emulate(), ip);

                try
                {
                    var telemetry = Telemetry.AsDictionary(result.Buffer);

                    var data = new Proto.Data();
                    foreach (var field in _streamingFields)
                        data.Values.Add(field, telemetry[field]);

                    //Console.WriteLine(
                    //    $"Time: {telemetry[Telemetry.Fields[0]]:F2}, " +
                    //    $"Speed: {telemetry[Telemetry.Fields[7]]:F2}");

                    _baseService?.Publish(data);
                }
                catch (ArgumentException ex)
                {
                    Console.WriteLine($"Invalid packet: {ex.Message}");
                }
            }
        }
        catch { }
    }

    #endregion
}
