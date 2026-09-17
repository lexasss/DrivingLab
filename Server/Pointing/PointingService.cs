using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using SharpDX.DirectInput;
using Proto = global::Pointing;

namespace Server.Pointing;

internal class PointingService : Proto.Dispatcher.DispatcherBase, ITelemetryService
{
    public bool IsAvailable() => true;

    public PointingService(ILoggerFactory loggerFactory) : base()
    {
        _logger = loggerFactory.CreateLogger("PNTG");

        try
        {
            foreach (var device in PointingDevice.ListDevices(DeviceType.Mouse))
                _logger.LogInformation("Found a mouse {device}", device.ProductName);
            foreach (var device in PointingDevice.ListDevices(DeviceType.Joystick))
                _logger.LogInformation("Found a joystick {device}", device.ProductName);
            foreach (var device in PointingDevice.ListDevices(DeviceType.Gamepad))
                _logger.LogInformation("Found a gamepad {device}", device.ProductName);

            _baseService = new(_logger);
        }
        catch (Exception)
        {
            _logger.LogError("Cannot start the service");
        }
    }

    public void Dispose()
    {
        _device?.Dispose();
        _baseService?.Dispose();

        _device = null;

        GC.SuppressFinalize(this);
    }

    public override Task<Common.Bool> IsAvailable(
        Empty request,
        ServerCallContext context)
    {
        return Common.Bool.From(IsAvailable());
    }

    public override Task<Proto.Devices> GetDevices(
        Proto.DeviceRequest request,
        ServerCallContext context)
    {
        var type = ToDirectInputType(request.Type);
        var devices = PointingDevice.ListDevices(type);

        var result = new Proto.Devices();
        foreach (var device in devices)
        {
            result.Items.Add(new Proto.Device()
            {
                Type = request.Type,
                Name = device.ProductName.Equals(request.Type.ToString())
                    ? string.Empty          // Mouse will be simply "Mouse", so lets not have it as a name, only as a type
                    : device.ProductName
            });
        }

        return Task.FromResult(result);
    }

    public override Task<Common.Bool> SetPointingDevice(
        Proto.Device request,
        ServerCallContext context)
    {
        var type = ToDirectInputType(request.Type);
        var result = PointingDevice.Has(type);

        if (result)
        {
            _device?.Dispose();
            _device = request.Type switch
            {
                Proto.DeviceType.Mouse => new Mouse(),
                Proto.DeviceType.Joystick => new Joystick(request.Name),
                Proto.DeviceType.Gamepad => new Gamepad(request.Name),
                _ => throw new NotImplementedException()
            };
            _device.Data += Device_Data;
            _device.Disconnected += Device_Disconnected;
        }

        return Common.Bool.From(result);
    }

    public override Task<Empty> Start(
        Empty request,
        ServerCallContext context)
    {
        if (_baseService?.IsSending == false)
        {
            _device?.Reset();
            _baseService.Start();
        }
        return Common.Constants.Empty;
    }

    public override Task<Empty> Stop(
        Empty request,
        ServerCallContext context)
    {
        _baseService?.Stop();
        return Common.Constants.Empty;
    }

    public override Task<Common.Bool> SetLogFileName(
        Common.String request,
        ServerCallContext context)
    {
        if (_baseService == null)
            return Common.Bool.False;

        _device?.Reset();

        return _baseService.SetLogFileName(request.Value);
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

    #region Internal

    readonly ILogger _logger;
    readonly Tools.TelemetryService<Proto.Data, Proto.Event>? _baseService;

    PointingDevice? _device;

    private DeviceType ToDirectInputType(Proto.DeviceType type) =>
        type switch
        {
            Proto.DeviceType.Mouse => DeviceType.Mouse,
            Proto.DeviceType.Joystick => DeviceType.Joystick,
            Proto.DeviceType.Gamepad => DeviceType.Gamepad,
            _ => throw new NotImplementedException()
        };

    // Event handlers

    private void Device_Data(object? sender, Proto.Data data)
    {
        _baseService?.Publish(data);
    }

    private void Device_Disconnected(object? sender, EventArgs e)
    {
        _baseService?.Publish(new Proto.Event() {
            IsConnected = false
        });
    }

    #endregion
}