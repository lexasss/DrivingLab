using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;
using Channel = System.Threading.Channels.Channel;
using Proto = global::Gaze;

namespace Server.MyGaze;

internal class MyGazeService :
    Proto.Dispatcher.DispatcherBase,
    ITelemetryService
{
    public bool IsAvailable() => _myGaze != null;

    public MyGazeService(ILoggerFactory loggerFactory) : base()
    {
        _logger = loggerFactory.CreateLogger("VIMG");

        try
        {
            _myGaze = new MyGaze(_logger);
            _myGaze.Event += MyGaze_Event;
            _myGaze.Sample += MyGaze_Sample;

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

        _myGaze?.Dispose();
        _myGaze = null;

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

    public override Task<Empty> Start(
        Empty request,
        ServerCallContext context)
    {
        if (!_isSending)
        {
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
        return Tools.TelemetryService.SetLogFileName(request.Value, _fileLogger, _logger);
    }

    public override async Task ReadData(
        Empty request,
        IServerStreamWriter<Proto.Sample> responseStream,
        ServerCallContext context)
    {
        if (_myGaze == null || _isReading)
            return;

        _myGaze.Start();
        if (!_myGaze.IsTracking)
        {
            _logger.LogError("Data reading: failed");
            return;
        }

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
        catch (Exception)
        { }
        finally
        {
            _myGaze?.Stop();
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
    readonly Channel<Proto.Sample> _channel = Channel.CreateUnbounded<Proto.Sample>();
    readonly Tools.FileLogger _fileLogger = new();

    MyGaze? _myGaze;

    bool _isActive = false;
    bool _isReading = false;
    bool _isSending = false;

    // Event handlers

    private void MyGaze_Event(object? sender, MyGazeAPI.EventStruct e)
    {
        switch (e.eventType)
        {
            default:
                System.Diagnostics.Debug.WriteLine($"MyGaze event '{e.eventType}'");
                break;
        }
    }

    private void MyGaze_Sample(object? sender, MyGazeAPI.SampleStruct sample)
    {
        var data = new Proto.Sample
        {
            Timestamp = sample.timestamp,
            EyeXL = sample.leftEye.gazeX,
            EyeYL = sample.leftEye.gazeY,
            EyeXR = sample.rightEye.gazeX,
            EyeYR = sample.rightEye.gazeY
        };

        if (data.EyeXL > MyGazeAPI.MIN_VALID_COORD && data.EyeXR > MyGazeAPI.MIN_VALID_COORD)
        {
            data.ValidEye = Proto.Sample.Types.Eye.Both;
            data.EyeX = (data.EyeXL + data.EyeXR) / 2;
            data.EyeY = (data.EyeYL + data.EyeYR) / 2;
        }
        else if (data.EyeXL > MyGazeAPI.MIN_VALID_COORD)
        {
            data.ValidEye = Proto.Sample.Types.Eye.Left;
            data.EyeX = data.EyeXL;
            data.EyeY = data.EyeYL;
        }
        else if (data.EyeXR > MyGazeAPI.MIN_VALID_COORD)
        {
            data.ValidEye = Proto.Sample.Types.Eye.Right;
            data.EyeX = data.EyeXR;
            data.EyeY = data.EyeYR;
        }
        else
        {
            data.ValidEye = Proto.Sample.Types.Eye.None;
            data.EyeX = 0;
            data.EyeY = 0;
        }

        _channel.Writer.TryWrite(data);
    }

    #endregion
}