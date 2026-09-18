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
            _wheelBase = Create(
                "Simucube 2 Pro",
                DeviceType.FirstPerson,
                WheelBase_Data,
                WheelBase_Disconnected);
            _pedals = Create(
                "Meca",
                DeviceType.Supplemental,
                Pedals_Data,
                Pedals_Disconnected);
            _activePedals = Create(
                "SC-Link",
                DeviceType.FirstPerson,
                ActivePedals_Data,
                ActivePedals_Disconnected);

            _connectionStatus.IsWheelConnected = _wheelBase != null;
            _connectionStatus.IsBaseConnected = _wheelBase != null;
            _connectionStatus.ArePedalsConnected = _pedals != null;
            _connectionStatus.AreActivePedalsConnected = _activePedals != null;

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

    private Pointing.Joystick? Create(
        string name,
        DeviceType type,
        EventHandler<global::Pointing.Data> dataHandler,
        EventHandler disconnectionHandler)
    {
        Pointing.Joystick? result = null;
        var controller = new Pointing.Joystick(
            name, type,
            Pointing.Joystick.NameComparisionOption.StartsWith);
        if (controller.IsCreated)
        {
            result = controller;
            result.Data += dataHandler;
            result.Disconnected += disconnectionHandler;

            _logger.LogInformation("{name} connected", name);
        }
        else
        {
            _logger.LogWarning("Failed to connect to {name}", name);
            controller.Dispose();
        }

        return result;
    }

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
        _data.ActiveBrakePedal = data.Rotation.Z;
        _data.ActiveThrottlePedal = data.Rotation.X;
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