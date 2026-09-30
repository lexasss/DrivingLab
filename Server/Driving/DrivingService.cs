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
            _wheelBase = CreateController(
                "Simucube 2 Pro",
                DeviceType.FirstPerson,
                WheelBase_Data,
                WheelBase_Disconnected);
            _pedals = CreateController(
                "Meca",
                DeviceType.Supplemental,
                Pedals_Data,
                Pedals_Disconnected);
            _activePedals = CreateController(
                "SC-Link",
                DeviceType.FirstPerson,
                ActivePedals_Data,
                ActivePedals_Disconnected);

            _connectionStatus.IsWheelConnected = _wheelBase != null;
            _connectionStatus.IsBaseConnected = _wheelBase != null;
            _connectionStatus.ArePedalsConnected = _pedals != null;
            _connectionStatus.IsActivePedalsHubConnected = _activePedals != null;
            _connectionStatus.ActivePedalsConnected = Proto.ActivePedal.None;

            _logger.LogInformation("Running");

            _baseService = new(_logger);

            Task.Run(async () =>
            {
                await Task.Delay(500);

                _connectionStatus.ActivePedalsConnected = SimucubeApi.Init(2);
                _baseService?.Publish(new Proto.Event()
                {
                    ConnectionStatus = _connectionStatus
                });

                if (_connectionStatus.ActivePedalsConnected.HasFlag(Proto.ActivePedal.Brake))
                    _logger.LogInformation("Found pedal BRAKE");
                if (_connectionStatus.ActivePedalsConnected.HasFlag(Proto.ActivePedal.Throttle))
                    _logger.LogInformation("Found pedal THROTTLE");
                if (_connectionStatus.ActivePedalsConnected == Proto.ActivePedal.None)
                    _logger.LogWarning("Found no pedals (is Simucube Tuner running and the pedals are activated?)");
            });
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
        return Common.Awaitable.From(IsAvailable());
    }

    public override Task<Proto.ConnectionStatus> GetConnectionStatus(
        Empty request,
        ServerCallContext context)
    {
        return Task.FromResult(_connectionStatus);
    }

    public override Task<Proto.PeriodicEffectParameters> GetPeriodicEffectParameters(
        Empty request,
        ServerCallContext context)
    {
        return Task.FromResult(_periodicEffectParams);
    }

    public override Task<Common.Bool> SetPeriodicEffectParameters(
        Proto.PeriodicEffectParameters request,
        ServerCallContext context)
    {
        if (!_isPlayingEffect)
        {
            _periodicEffectParams = request;
            return Common.Awaitable.True;
        }

        return Common.Awaitable.False;
    }

    public override Task<Common.Bool> PlayPedalEffect(
        Proto.PedalEffect request,
        ServerCallContext context)
    {
        if (_isPlayingEffect)
            return Common.Awaitable.False;

        if (_connectionStatus.ActivePedalsConnected.HasFlag(request.Pedal))
        {
            _isPlayingEffect = true;
            _logger.LogInformation("Playing {type} {var} = {amplitude} for {duration} ms on {pedal}",
                request.Type == Proto.EffectType.Constant
                    ? "Constant"
                    : $"{_periodicEffectParams.Type} {_periodicEffectParams.Frequency} Hz",
                request.Variable,
                request.Amplitude,
                request.Duration,
                request.Pedal);
            SimucubeApi.Configure(request.Pedal, request.Variable);
            if (request.Type == Proto.EffectType.Periodic)
                SimucubeApi.ConfigurePeriodic(
                    _periodicEffectParams.Type,
                    _periodicEffectParams.Frequency);

            Task.Run(() =>  // SimucubeApi.Run is a blocking function
            {
                SimucubeApi.Run(
                    request.Pedal,
                    request.Type,
                    request.Duration,
                    request.Amplitude);

                _isPlayingEffect = false;
                _baseService?.Publish(new Proto.Event()
                {
                    EffectFinished = new Empty()
                });
            });
        }
        else
        {
            _logger.LogError("Pedal {pedal} is not active", request.Pedal);
            return Common.Awaitable.False;
        }

        return Common.Awaitable.True;
    }

    public override Task<Empty> StopPedalEffect(
        Proto.StopEffect request,
        ServerCallContext context)
    {
        SimucubeApi.Stop(request.Pedal);
        return Common.Awaitable.Empty;
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
        return Common.Awaitable.Empty;
    }

    public override Task<Empty> Stop(Empty request, ServerCallContext context)
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

    const double ROTARY_BUTTON_STEP = 1.0 / 18;
    const double ROTARY_TILT_BUTTON_STEP = 1.0 / 30;

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
    readonly Proto.ConnectionStatus _connectionStatus = new()
    {
        IsBaseConnected = false,
        IsWheelConnected = false,
        ArePedalsConnected = false,
        IsActivePedalsHubConnected = false,
        ActivePedalsConnected = Proto.ActivePedal.None
    };

    Pointing.Controller? _wheelBase;
    Pointing.Controller? _pedals;
    Pointing.Controller? _activePedals;

    Proto.PeriodicEffectParameters _periodicEffectParams = new() {
        Type = Proto.PeriodicEffectType.Sine,
        Frequency = 20
    };
    bool _isPlayingEffect = false;

    private Pointing.Joystick? CreateController(
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

        foreach (var btn in data.Buttons)
        {
            // Buttons
            if (btn.Id == 24)
                _data.TopLeftButton1.IsPressed = btn.IsPressed;
            else if (btn.Id == 23)
                _data.TopLeftButton2.IsPressed = btn.IsPressed;
            else if (btn.Id == 19)
                _data.TopLeftButton3.IsPressed = btn.IsPressed;
            else if (btn.Id == 11)
                _data.TopRightButton1.IsPressed = btn.IsPressed;
            else if (btn.Id == 20)
                _data.TopRightButton2.IsPressed = btn.IsPressed;
            else if (btn.Id == 10)
                _data.TopRightButton3.IsPressed = btn.IsPressed;
            else if (btn.Id == 35)
                _data.BottomLeftButton1.IsPressed = btn.IsPressed;
            else if (btn.Id == 25)
                _data.BottomLeftButton2.IsPressed = btn.IsPressed;
            else if (btn.Id == 31)
                _data.RotaryButton.IsPressed = btn.IsPressed;
            else if (btn.Id == 14)
                _data.BottomRightButton1.IsPressed = btn.IsPressed;
            else if (btn.Id == 28)
                _data.BottomRightButton2.IsPressed = btn.IsPressed;
            else if (btn.Id == 29)
                _data.RotaryTiltButton.IsPressed = btn.IsPressed;
            else if (btn.Id == 8)
                _data.LeftPaddleShifter.IsPressed = btn.IsPressed;
            else if (btn.Id == 9)
                _data.RightPaddleShifter.IsPressed = btn.IsPressed;

            // Rotating Rotation and Rotation-Tilt knobs
            else if (btn.Id == 16)
            {
                _data.RotaryButton.RotationDirection = btn.IsPressed
                    ? Proto.RotationDirectin.Left
                    : Proto.RotationDirectin.Null;
                if (btn.IsPressed)
                    _data.RotaryButton.RotationValue -= ROTARY_BUTTON_STEP;
            }
            else if (btn.Id == 17)
            {
                _data.RotaryButton.RotationDirection = btn.IsPressed
                    ? Proto.RotationDirectin.Right
                    : Proto.RotationDirectin.Null;
                if (btn.IsPressed)
                    _data.RotaryButton.RotationValue += ROTARY_BUTTON_STEP;
            }
            else if (btn.Id == 12)
            {
                _data.RotaryTiltButton.RotationDirection = btn.IsPressed
                    ? Proto.RotationDirectin.Right
                    : Proto.RotationDirectin.Null;
                if (btn.IsPressed)
                    _data.RotaryTiltButton.RotationValue -= ROTARY_TILT_BUTTON_STEP;
            }
            else if (btn.Id == 13)
            {
                _data.RotaryTiltButton.RotationDirection = btn.IsPressed
                    ? Proto.RotationDirectin.Right
                    : Proto.RotationDirectin.Null;
                if (btn.IsPressed)
                    _data.RotaryTiltButton.RotationValue += ROTARY_TILT_BUTTON_STEP;
            }

            // Tilting Rotation-Tilt knob
            else if (btn.Id == 15)
            {
                _data.RotaryTiltButton.IsTilted = btn.IsPressed;
                if (btn.IsPressed)
                    _data.RotaryTiltButton.Degrees = 0;
            }
            else if (btn.Id == 30)
            {
                _data.RotaryTiltButton.IsTilted = btn.IsPressed;
                if (btn.IsPressed)
                    _data.RotaryTiltButton.Degrees = 90;
            }
            else if (btn.Id == 21)
            {
                _data.RotaryTiltButton.IsTilted = btn.IsPressed;
                if (btn.IsPressed)
                    _data.RotaryTiltButton.Degrees = 180;
            }
            else if (btn.Id == 22)
            {
                _data.RotaryTiltButton.IsTilted = btn.IsPressed;
                if (btn.IsPressed)
                    _data.RotaryTiltButton.Degrees = 270;
            }
        }

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
        _connectionStatus.IsActivePedalsHubConnected = false;
        _baseService?.Publish(new Proto.Event() {
            ConnectionStatus = _connectionStatus
        });
    }

    #endregion
}