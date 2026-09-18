using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using SharpDX.DirectInput;
using Proto = global::Driving;

namespace Server.Driving;

internal class DrivingService :
    Proto.Dispatcher.DispatcherBase,
    ITelemetryService
{
    public bool IsAvailable() => true;

    public DrivingService(ILoggerFactory loggerFactory) : base()
    {
        _logger = loggerFactory.CreateLogger("DRIV");

        try
        {
            var wheelBase = new Pointing.Joystick(
                "Simucube 2 Pro", 
                DeviceType.FirstPerson,
                Pointing.Joystick.NameComparisionOption.StartsWith);
            if (wheelBase.IsCreated)
            {
                _wheelBase = wheelBase;
                _wheelBase.Data += WheelBase_Data;
                _wheelBase.Disconnected += WheelBase_Disconnected;

                _connectionStatus.IsWheelConnected = true;
                _connectionStatus.IsBaseConnected = true;

                _logger.LogInformation("Wheel and its base connected");
            }
            else
            {
                _logger.LogWarning("Failed to connect to the wheel and its base");
                wheelBase.Dispose();
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
                _logger.LogWarning("Failed to connect to the pedals");
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
            else
            {
                _logger.LogWarning("Failed to connect to the active pedals");
                activePedals.Dispose();
            }

            _logger.LogInformation("Running");

            _baseService = new(_logger);
        }
        catch (Exception)
        {
            _logger.LogError("Cannot start the service");
        }
    }

    public void Dispose()
    {
        _wheelBase?.Dispose();
        _pedals?.Dispose();
        _activePedals?.Dispose();

        _baseService?.Dispose();

        _wheelBase = null;
        _pedals = null;
        _activePedals = null;

        GC.SuppressFinalize(this);
    }

    public override Task<Common.Bool> IsAvailable(
        Empty request,
        ServerCallContext context)
    {
        return Common.Bool.From(IsAvailable());
    }

    public override Task<Proto.ConnectionStatus> GetConnectionStatus(
        Empty request,
        ServerCallContext context)
    {
        return Task.FromResult(_connectionStatus);
    }

    public override Task<Empty> SetActivePedalProfile(
        Proto.ActivePedalProfile request,
        ServerCallContext context)
    {
        // TODO
        return Common.Constants.Empty;
    }

    public override Task<Empty> Start(Empty request, ServerCallContext context)
    {
        if (_baseService?.IsSending == false)
        {
            _wheelBase?.Reset();
            _pedals?.Reset();
            _activePedals?.Reset();

            _baseService.Start();
        }
        return Common.Constants.Empty;
    }

    public override Task<Empty> Stop(Empty request, ServerCallContext context)
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

        _wheelBase?.Reset();
        _pedals?.Reset();
        _activePedals?.Reset();

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
    readonly Proto.Data _data = new()
    {
        TopLeftButton1 = new Proto.Button(),
        TopLeftButton2 = new Proto.Button(),
        TopLeftButton3 = new Proto.Button(),
        TopRightButton1 = new Proto.Button(),
        TopRightButton2 = new Proto.Button(),
        TopRightButton3 = new Proto.Button(),
        BottomLeftButton1 = new Proto.Button(),
        BottomLeftButton2 = new Proto.Button(),
        BottomRightButton1 = new Proto.Button(),
        BottomRightButton2 = new Proto.Button(),
        LeftPaddleShifter = new Proto.Button(),
        RightPaddleShifter = new Proto.Button(),
        RotaryButton = new Proto.RotaryButton(),
        RotaryTiltButton = new Proto.RotaryTiltButton(),
    };

    Pointing.Controller? _wheelBase;
    Pointing.Controller? _pedals;
    Pointing.Controller? _activePedals;


    Proto.ConnectionStatus _connectionStatus = new() {
        IsBaseConnected = false,
        IsWheelConnected = false,
        ArePedalsConnected = false,
        AreActivePedalsConnected = false
    };

    // Event handlers

    private void WheelBase_Data(object? sender, global::Pointing.Data data)
    {
        _data.WheelRotation = data.Point.X;
        _baseService?.Publish(_data);
    }

    private void WheelBase_Disconnected(object? sender, EventArgs e)
    {
        _connectionStatus.IsBaseConnected = false;
        _connectionStatus.IsWheelConnected = false;
        _baseService?.Publish(new Proto.Event() {
            ConnectionStatus = _connectionStatus
        });
    }

    private void Pedals_Data(object? sender, global::Pointing.Data data)
    {
        _data.BrakePedal = data.Rotation.Y;
        _data.ThrottlePedal = data.Rotation.Z;
        _baseService?.Publish(_data);
    }

    private void Pedals_Disconnected(object? sender, EventArgs e)
    {
        _connectionStatus.ArePedalsConnected = false;
        _baseService?.Publish(new Proto.Event() {
            ConnectionStatus = _connectionStatus
        });
    }

    private void ActivePedals_Data(object? sender, global::Pointing.Data data)
    {
        _data.ActiveBrakePedal = data.Rotation.Y;
        _data.ActiveThrottlePedal = data.Rotation.Z;
        _baseService?.Publish(_data);
    }

    private void ActivePedals_Disconnected(object? sender, EventArgs e)
    {
        _connectionStatus.AreActivePedalsConnected = false;
        _baseService?.Publish(new Proto.Event() {
            ConnectionStatus = _connectionStatus
        });
    }

    #endregion
}