using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using SharpDX.DirectInput;
using System.Threading.Channels;
using Channel = System.Threading.Channels.Channel;
using Proto = global::Driving;

namespace Server.Driving;

internal class DrivingService : Proto.Dispatcher.DispatcherBase, ITelemetryService
{
    public bool IsAvailable() => true;

    public DrivingService(ILoggerFactory loggerFactory) : base()
    {
        _logger = loggerFactory.CreateLogger("DRIV");

        try
        {
            var wheel = new Pointing.Joystick(
                "Simucube 2 Pro", 
                DeviceType.FirstPerson,
                Pointing.Joystick.NameComparisionOption.StartsWith);
            if (wheel.IsCreated)
            {
                _wheel = wheel;
                _wheel.Data += Wheel_Data;
                _wheel.Disconnected += Wheel_Disconnected;
                _connectionStatus.IsWheelConnected = true;
                _connectionStatus.IsBaseConnected = true;
                _logger.LogInformation("Wheel connected");
            }
            else
            {
                wheel.Dispose();
            }

            var pedals = new Pointing.Joystick(
                "Meca",
                DeviceType.Supplemental,
                Pointing.Joystick.NameComparisionOption.StartsWith);
            if (pedals.IsCreated)
            {
                _pedals = pedals;
                _pedals.Data += Pedals_Data;
                _pedals.Disconnected += Pedals_Disconnected;
                _connectionStatus.ArePedalsConnected = true;
                _logger.LogInformation("Pedals connected");
            }
            else
            {
                pedals.Dispose();
            }

            var activePedals = new Pointing.Joystick(
                "Simucube Pedal",
                DeviceType.Supplemental,
                Pointing.Joystick.NameComparisionOption.StartsWith);
            if (activePedals.IsCreated)
            {
                _activePedals = activePedals;
                _activePedals.Data += ActivePedals_Data;
                _activePedals.Disconnected += ActivePedals_Disconnected;
                _connectionStatus.AreActivePedalsConnected = true;
                _logger.LogInformation("Active pedals connected");
            }

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

        _wheel?.Dispose();
        _pedals?.Dispose();
        _activePedals?.Dispose();

        _wheel = null;
        _pedals = null;
        _activePedals = null;

        _fileLogger.Dispose();
        _logger.LogInformation("Disposed");

        GC.SuppressFinalize(this);
    }

    public override Task<Common.Bool> IsAvailable(Empty request, ServerCallContext context)
    {
        return Task.FromResult(new Common.Bool() { Value = IsAvailable() });
    }

    public override Task<Proto.ConnectionStatus> GetConnectionAvailable(Empty request, ServerCallContext context)
    {
        return Task.FromResult(_connectionStatus);
    }

    public override Task<Empty> SetActivePedalProfile(Proto.ActivePedalProfile request, ServerCallContext context)
    {
        return Task.FromResult(new Empty());
    }

    public override Task<Empty> Start(Empty request, ServerCallContext context)
    {
        if (!_isSending)
        {
            _wheel?.Reset();
            _pedals?.Reset();
            _activePedals?.Reset();

            _logger.LogInformation("Data streaming: started");
            _isSending = true;
        }
        return Task.FromResult(new Empty());
    }

    public override Task<Empty> Stop(Empty request, ServerCallContext context)
    {
        if (_isSending)
        {
            _logger.LogInformation("Data streaming: stopped");
            _isSending = false;
        }
        return Task.FromResult(new Empty());
    }

    public override Task<Common.Bool> SetLogFileName(Common.String request, ServerCallContext context)
    {
        _wheel?.Reset();
        _pedals?.Reset();
        _activePedals?.Reset();

        return Tools.TelemetryService.SetLogFileName(request.Value, _fileLogger, _logger);
    }

    public override async Task ReadData(Empty request, IServerStreamWriter<Proto.Data> responseStream, ServerCallContext context)
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

    public override async Task ReadEvents(Empty request, IServerStreamWriter<Proto.Event> responseStream, ServerCallContext context)
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
    readonly Proto.Data _data = new();

    Pointing.PointingDevice? _wheel;
    Pointing.PointingDevice? _pedals;
    Pointing.PointingDevice? _activePedals;

    bool _isActive = false;
    bool _isReading = false;
    bool _isSending = false;

    Proto.ConnectionStatus _connectionStatus = new() {
        IsBaseConnected = false,
        IsWheelConnected = false,
        ArePedalsConnected = false,
        AreActivePedalsConnected = false
    };


    // Event handlers

    private void Wheel_Data(object? sender, global::Pointing.Data data)
    {
        _data.WheelRotation = data.Point.X;
        _channel.Writer.TryWrite(_data);
    }

    private void Wheel_Disconnected(object? sender, EventArgs e)
    {
        _connectionStatus.IsBaseConnected = false;
        _connectionStatus.IsWheelConnected = false;
        _events.Enqueue(new Proto.Event() { ConnectionStatus = _connectionStatus });
    }

    private void Pedals_Data(object? sender, global::Pointing.Data data)
    {
        _data.BrakePedal = data.Rotation.Y;
        _data.ThrottlePedal = data.Rotation.Z;
        _channel.Writer.TryWrite(_data);
    }

    private void Pedals_Disconnected(object? sender, EventArgs e)
    {
        _connectionStatus.ArePedalsConnected = false;
        _events.Enqueue(new Proto.Event() { ConnectionStatus = _connectionStatus });
    }

    private void ActivePedals_Data(object? sender, global::Pointing.Data data)
    {
        _data.ActiveBrakePedal = data.Rotation.Y;
        _data.ActiveThrottlePedal = data.Rotation.Z;
        _channel.Writer.TryWrite(_data);
    }

    private void ActivePedals_Disconnected(object? sender, EventArgs e)
    {
        _connectionStatus.AreActivePedalsConnected = false;
        _events.Enqueue(new Proto.Event() { ConnectionStatus = _connectionStatus });
    }

    #endregion
}