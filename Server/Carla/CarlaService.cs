using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Server.Settings;
using System.Net;
using System.Net.Sockets;
using Proto = global::Carla;

namespace Server.Carla;

internal class CarlaService : 
    Proto.Dispatcher.DispatcherBase,
    ITelemetryService
{
    public bool IsAvailable() => true;

    public CarlaService(
        ILoggerFactory loggerFactory,
        CarlaSettings carlaSettings) : base()
    {
        _logger = loggerFactory.CreateLogger("CARL");
        _logger.LogInformation("Running");

        _baseService = new(_logger);
        _isEmulating = carlaSettings.IsEmulating;

        try
        {
            var ip = IPAddress.Parse(carlaSettings.Ip);
            _udp = new UdpClient(new IPEndPoint(ip, carlaSettings.Port));
            if (_isEmulating)
                _logger.LogInformation("Emulating telemetry data");
            else
                _logger.LogInformation("Listening on {ip}:{port}",
                    carlaSettings.Ip,
                    carlaSettings.Port);

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

    readonly ILogger _logger;
    readonly Tools.TelemetryService<Proto.Data, Proto.Event>? _baseService;
    readonly UdpClient? _udp;
    readonly bool _isEmulating = false;
    readonly IPEndPoint _emulatorEndPoint = IPEndPoint.Parse("127.0.0.1");

    readonly List<string> _streamingFields = [];

    private async Task RunListenerLoop(UdpClient udp)
    {
        try
        {
            while (true)
            {
                UdpReceiveResult result = _isEmulating
                    ? new UdpReceiveResult(Telemetry.Emulate(), _emulatorEndPoint)
                    : await udp.ReceiveAsync();

                if (_isEmulating)
                    await Task.Delay(20);

                try
                {
                    var telemetry = Telemetry.AsDictionary(result.Buffer);

                    var data = new Proto.Data();
                    foreach (var field in _streamingFields)
                        data.Values.Add(field, telemetry[field]);

                    if (_isEmulating)
                        Console.WriteLine($"Time = {telemetry["total_time"]:F1}"
                            + $", Dist = {telemetry["total_distance"]:F0}"
                            + $", Speed = {telemetry["speed"]:F1}"
                            + $", RPM = {telemetry["engine_rate"]:F0}"
                            + "    "
                            + $", Lap = {telemetry["lap"]}"
                            + $", LapTime = {telemetry["lap_time"]:F1}"
                            + $", LapDist = {telemetry["lap_distance"]:F0}"
                        );

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
