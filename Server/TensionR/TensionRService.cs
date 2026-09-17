using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using API = TensionR.API;
using Proto = global::TensionR;

namespace Server.TensionR;

internal class TensionRService :
    Proto.Dispatcher.DispatcherBase,
    ITelemetryService
{
    public bool IsAvailable() => true;

    public TensionRService(ILoggerFactory loggerFactory) : base()
    {
        _logger = loggerFactory.CreateLogger("BELT");

        _baseService = new(_logger);
    }

    public void Dispose()
    {
        _belt?.Dispose();
        _baseService?.Dispose();
        _fileLogger.Dispose();

        _belt = null;

        GC.SuppressFinalize(this);
    }

    public override Task<Common.Bool> IsAvailable(
        Empty request,
        ServerCallContext context)
    {
        return Common.Bool.From(IsAvailable());
    }

    public override Task<Common.Bool> IsConnected(
        Empty request,
        ServerCallContext context)
    {
        return Common.Bool.From(_belt?.IsConnected ?? false);
    }

    public override Task<Common.Bool> IsEnabled(
        Empty request,
        ServerCallContext context)
    {
        return Common.Bool.From(_isEnabled);
    }

    public override Task<Common.Bool> IsCalibrated(
        Empty request,
        ServerCallContext context)
    {
        return Common.Bool.From(_isCalibrated);
    }

    public override Task<Common.Bool> Connect(
        Common.String request,
        ServerCallContext context)
    {
        if (_belt != null)
            return Common.Bool.False;

        bool isConnected = false;

        _belt = new API.Belt(request.Value);
        isConnected = _belt.IsConnected;

        if (!_belt.IsConnected)
        {
            _logger.LogError("Failed to connect to {port}", request.Value);

            _belt.Dispose();
            _belt = null;
        }
        else
        {
            _logger.LogInformation("Connected to {name}", request.Value);

            _belt.Error += Belt_Error;
            _belt.RequestSent += Belt_RequestSent;
            _belt.DataReceived += Belt_DataReceived;

            _baseService.Publish(new Proto.Event() {
                IsConnected = true
            });
        }

        return Common.Bool.From(isConnected);
    }

    public override Task<Common.Bool> SetLogFileName(
        Common.String request,
        ServerCallContext context)
    {
        return Tools.TelemetryHelper.SetLogFileName(
            request.Value,
            _fileLogger,
            _logger);
    }

    public override Task<Empty> Start(
        Empty request,
        ServerCallContext context)
    {
        if (_belt != null && !_isEnabled && !_isCalibrating)
        {
            _belt.Comm.Start();

            _logger.LogInformation("Activated");
            _isEnabled = true;

            _baseService.Publish(new Proto.Event() {
                IsEnabled = _isEnabled
            });
        }
        return Common.Constants.Empty;
    }

    public override Task<Empty> Stop(
        Empty request,
        ServerCallContext context)
    {
        if (_belt != null && _isEnabled && !_isCalibrating)
        {
            _belt.Comm.Stop();

            _logger.LogInformation("Deactivated");
            _isEnabled = false;

            _baseService.Publish(new Proto.Event() {
                IsEnabled = _isEnabled
            });
        }
        return Common.Constants.Empty;
    }

    public override Task<Empty> Calibrate(
        Empty request,
        ServerCallContext context)
    {
        if (_belt != null && _isEnabled && !_isCalibrating)
        {
            _isCalibrating = true;

            _belt.Comm.Calibrate();

            _logger.LogInformation("Calibrating");
        }

        return Common.Constants.Empty;
    }

    public override Task<Empty> SetTension(
        Proto.Tension request,
        ServerCallContext context)
    {
        if (_belt != null && _isEnabled && _isCalibrated)
        {
            _belt.Comm.SetTension(
                request.Value,
                request.Side switch {
                    Proto.Side.Left => API.Side.Left,
                    Proto.Side.Right => API.Side.Right,
                    _ => API.Side.Both,
                }
            );
            _logger.LogInformation("Tension {value} ({side})",
                request.Value,
                request.Side);
        }

        return Common.Constants.Empty;
    }

    public override async Task ReadEvents(
        Empty request,
        IServerStreamWriter<Proto.Event> responseStream,
        ServerCallContext context)
    {
        if (_baseService == null)
            return;

        await _baseService.ReadEvents(request, responseStream, context);

        _isEnabled = false;
        _isCalibrating = false;
        _isCalibrated = false;

        _belt?.Dispose();
        _belt = null;
    }

    #region Internal

    readonly ILogger _logger;
    readonly Tools.Service<Proto.Event> _baseService;
    readonly Tools.FileLogger _fileLogger = new();

    API.Belt? _belt;

    bool _isEnabled = false;
    bool _isCalibrating = false;
    bool _isCalibrated = false;

    private void Belt_Error(object? sender, Exception e)
    {
        _logger.LogError("Error: {ex}", e.Message);
    }

    private void Belt_RequestSent(object? sender, API.Communicator.RequestEventArgs e)
    {
        if (_isCalibrating && e.State == API.Communicator.State.CalibrationStop)
        {
            _isCalibrating = false;
            _isCalibrated = true;

            _logger.LogInformation($"Calibrated");

            _baseService.Publish(new Proto.Event() {
                IsCalibrated = true
            });
        }

        _fileLogger.Add("SND",
            e.State,
            e.Packets
             .SelectMany(p => p.ToBytes())
             .Select(b => $"{b:x2}")
        );
    }

    private void Belt_DataReceived(object? sender, API.In.Packet e)
    {
        if (e is API.In.PacketError error)
        {
            _logger.LogError("Error: {ex}", error.ToString());
        }

        _fileLogger.Add("RCV",
            e.ToBytes()
             .Select(b => $"{b:x2}")
        );
    }

    #endregion
}