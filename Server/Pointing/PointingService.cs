using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using SharpDX.DirectInput;
using System.Threading.Channels;
using Channel = System.Threading.Channels.Channel;
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

            _logger.LogInformation("Running");
            _isActive = true;
        }
        catch (Exception)
        {
            _logger.LogError("Cannot start the service");
        }
    }

    public void Dispose()
    {
        _isActive = false;

        _device?.Dispose();
        _device = null;

        _fileLogger.Dispose();
        _logger.LogInformation("Disposed");

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
        if (!_isSending)
        {
            _device?.Reset();
            _logger.LogInformation("Data streaming: started");
            _isSending = true;
        }

        return Common.Constants.Empty;
    }

    public override Task<Empty> Stop(
        Empty request,
        ServerCallContext context)
    {
        if (_isSending)
        {
            _logger.LogInformation("Data streaming: stopped");
            _isSending = false;
        }

        return Common.Constants.Empty;
    }

    public override Task<Common.Bool> SetLogFileName(
        Common.String request,
        ServerCallContext context)
    {
        _device?.Reset();
        return Tools.TelemetryService.SetLogFileName(request.Value, _fileLogger, _logger);
    }

    public override async Task ReadData(
        Empty request,
        IServerStreamWriter<Proto.Data> responseStream,
        ServerCallContext context)
    {
        if (_isReading)
            return;

        _logger.LogInformation("Data reading: started");
        _isReading = true;

        try
        {
            await foreach (var data in _channel.Reader.ReadAllAsync(context.CancellationToken))
            {
                if (_isSending)
                {
                    await responseStream.WriteAsync(data);
                    _fileLogger.Add(data.ToStringArray());
                }
            }
        }
        catch (Exception) { }
        finally
        {
            _logger.LogInformation("Data reading: stopped");
            _isReading = false;
        }
    }

    public override async Task ReadEvents(
        Empty request,
        IServerStreamWriter<Proto.Event> responseStream,
        ServerCallContext context)
    {
        while (_isActive && !context.CancellationToken.IsCancellationRequested)
        {
            await Task.Delay(5);

            if (_events.Count > 0)
            {
                var evt = _events.Dequeue();
                await responseStream.WriteAsync(evt);
            }
        }
    }

    #region Internal

    readonly ILogger _logger;
    readonly Queue<Proto.Event> _events = [];
    readonly Channel<Proto.Data> _channel = Channel.CreateUnbounded<Proto.Data>();
    readonly Tools.FileLogger _fileLogger = new();

    PointingDevice? _device;

    bool _isActive = false;
    bool _isReading = false;
    bool _isSending = false;

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
        _channel.Writer.TryWrite(data);
    }

    private void Device_Disconnected(object? sender, EventArgs e)
    {
        _events.Enqueue(new Proto.Event() {
            IsConnected = false
        });
    }

    #endregion
}