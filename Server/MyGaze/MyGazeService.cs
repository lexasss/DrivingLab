using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;
using Channel = System.Threading.Channels.Channel;

namespace Server.MyGaze;

internal class MyGazeService : Gaze.Dispatcher.DispatcherBase, ITelemetryService
{
    public bool IsAvailable() => _myGaze != null;

    public MyGazeService(ILogger<MyGazeService> logger) : base()
    {
        _logger = logger;

        try
        {
            _myGaze = new MyGaze(logger);
            _myGaze.Sample += MyGaze_Sample;

            _logger.LogInformation("[VIMG] Running");
        }
        catch (Exception)
        {
            _logger.LogCritical("[VIMG] Cannot start the service");
        }
    }

    public void Dispose()
    {
        _cts.Cancel();

        _myGaze?.Dispose();
        _myGaze = null;

        _fileLogger.Dispose();
        _logger.LogInformation("[VIMG] Disposed");
    }

    public override Task<Common.Bool> IsAvailable(Empty request, ServerCallContext context)
    {
        return Task.FromResult(new Common.Bool() { Value = IsAvailable() });
    }

    public override Task<Empty> Start(Empty request, ServerCallContext context)
    {
        if (!_isSending)
        {
            _logger.LogInformation("[VIMG] [req] Start");
            _isSending = true;
        }
        return Task.FromResult(new Empty());
    }

    public override Task<Empty> Stop(Empty request, ServerCallContext context)
    {
        if (_isSending)
        {
            _logger.LogInformation("[VIMG] [req] Stop");
            _isSending = false;
        }
        return Task.FromResult(new Empty());
    }

    public override Task<Common.Bool> SetLogFilename(Common.String request, ServerCallContext context)
    {
        _logger.LogInformation("[VIMG] [req] Logging to {filename}", request.Value);
        var result = _fileLogger.SetFilename(request.Value);
        return Task.FromResult(new Common.Bool() { Value = result });
    }

    public override async Task ReadData(Empty request, IServerStreamWriter<Gaze.Sample> responseStream, ServerCallContext context)
    {
        if (_myGaze == null || _isReading)
            return;

        _myGaze.Start();
        if (!_myGaze.IsTracking)
        {
            _logger.LogError("[VIMG] [req] Data reading: failed");
            return;
        }

        _logger.LogInformation("[VIMG] [req] Data reading: start");
        _isReading = true;

        await foreach (var data in _channel.Reader.ReadAllAsync(_cts.Token))
        {
            if (_isSending)
            {
                await responseStream.WriteAsync(data);
                _fileLogger.Add(data.ToStringArray());
            }
        }

        _myGaze.Stop();
        _logger.LogInformation("[VIMG] [---] Data reading: stop");
        _isReading = false;
    }

    #region Internal

    readonly ILogger<MyGazeService> _logger;
    readonly Channel<Gaze.Sample> _channel = Channel.CreateUnbounded<Gaze.Sample>();
    readonly CancellationTokenSource _cts = new();
    readonly Tools.FileLogger _fileLogger = new();

    MyGaze? _myGaze;

    bool _isReading = false;
    bool _isSending = false;

    // Event handlers

    private void MyGaze_Sample(object? sender, MyGazeAPI.SampleStruct sample)
    {
        var data = new Gaze.Sample
        {
            Timestamp = sample.timestamp,
            EyeXL = sample.leftEye.gazeX,
            EyeYL = sample.leftEye.gazeY,
            EyeXR = sample.rightEye.gazeX,
            EyeYR = sample.rightEye.gazeY
        };

        if (data.EyeXL > MyGazeAPI.MIN_VALID_COORD && data.EyeXR > MyGazeAPI.MIN_VALID_COORD)
        {
            data.ValidEye = Gaze.Sample.Types.Eye.Both;
            data.EyeX = (data.EyeXL + data.EyeXR) / 2;
            data.EyeY = (data.EyeYL + data.EyeYR) / 2;
        }
        else if (data.EyeXL > MyGazeAPI.MIN_VALID_COORD)
        {
            data.ValidEye = Gaze.Sample.Types.Eye.Left;
            data.EyeX = data.EyeXL;
            data.EyeY = data.EyeYL;
        }
        else if (data.EyeXR > MyGazeAPI.MIN_VALID_COORD)
        {
            data.ValidEye = Gaze.Sample.Types.Eye.Right;
            data.EyeX = data.EyeXR;
            data.EyeY = data.EyeYR;
        }
        else
        {
            data.ValidEye = Gaze.Sample.Types.Eye.None;
            data.EyeX = 0;
            data.EyeY = 0;
        }

        _channel.Writer.TryWrite(data);
    }

    #endregion
}