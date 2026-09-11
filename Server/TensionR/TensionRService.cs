using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using API = TensionR.API;
using Proto = global::TensionR;

namespace Server.TensionR;

internal class TensionRService : Proto.Dispatcher.DispatcherBase, ITelemetryService
{
    public bool IsAvailable() => true;

    public TensionRService(ILoggerFactory loggerFactory) : base()
    {
        _logger = loggerFactory.CreateLogger("BELT");

        try
        {
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

        _belt?.Dispose();
        _belt = null;

        _fileLogger.Dispose();
        _logger.LogInformation("Disposed");

        GC.SuppressFinalize(this);
    }

    public override Task<Common.Bool> IsAvailable(Empty request, ServerCallContext context)
    {
        return Task.FromResult(new Common.Bool() { Value = IsAvailable() });
    }

    public override Task<Common.Bool> Connect(Common.String request, ServerCallContext context)
    {
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
            _belt.Error += Belt_Error;
            _belt.RequestSent += Belt_RequestSent;
            _belt.DataReceived += Belt_DataReceived;

            _events.Enqueue(new Proto.Event() { IsConnected = true });
        }

        return Task.FromResult(new Common.Bool() { Value = isConnected });
    }

    public override Task<Common.Bool> SetLogFileName(Common.String request, ServerCallContext context)
    {
        var result = Tools.Helpers.SetLogFileName(request.Value, _fileLogger, _logger);
        return Task.FromResult(new Common.Bool { Value = result });
    }

    public override Task<Empty> Start(Empty request, ServerCallContext context)
    {
        if (_belt != null && !_isEnabled)
        {
            _belt.Comm.Start();

            _logger.LogInformation("Activated");
            _isEnabled = true;

            _events.Enqueue(new Proto.Event() { IsEnabled = _isEnabled });
        }
        return Task.FromResult(new Empty());
    }

    public override Task<Empty> Stop(Empty request, ServerCallContext context)
    {
        if (_belt != null && _isEnabled)
        {
            _belt.Comm.Stop();

            _logger.LogInformation("Deactivated");
            _isEnabled = false;

            _events.Enqueue(new Proto.Event() { IsEnabled = _isEnabled });
        }
        return Task.FromResult(new Empty());
    }

    public override Task<Empty> Calibrate(Empty request, ServerCallContext context)
    {
        if (_belt != null && _isEnabled && !_isCalibrating)
        {
            _isCalibrating = true;

            _belt.Comm.Calibrate();

            _logger.LogInformation("Calibrating");
        }

        return Task.FromResult(new Empty());
    }

    public override Task<Empty> SetTension(Common.Int request, ServerCallContext context)
    {
        if (_belt != null && _isEnabled && _isCalibrated)
        {
            _belt.Comm.SetTension(request.Value);
            _logger.LogInformation("Tension {value}", request.Value);
        }

        return Task.FromResult(new Empty());
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
    readonly Tools.FileLogger _fileLogger = new();

    API.Belt? _belt;

    bool _isActive = false;
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

            _events.Enqueue(new Proto.Event() { IsCalibrated = true });
        }

        _fileLogger.Add("SND", e.State, e.Packets
            .SelectMany(p => p.ToBytes())
            .Select(b => $"{b:x2}"));
    }

    private void Belt_DataReceived(object? sender, API.In.Packet e)
    {
        if (e is API.In.PacketError error)
        {
            _logger.LogError("Error: {ex}", error.ToString());
        }

        _fileLogger.Add("RCV", e.ToBytes()
            .Select(b => $"{b:x2}"));
    }

    #endregion
}