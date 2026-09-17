using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
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

            _baseService = new(_logger);
        }
        catch (Exception)
        {
            _logger.LogError("Cannot start the service");
        }
    }

    public void Dispose()
    {
        _myGaze?.Dispose();
        _baseService?.Dispose();

        _myGaze = null;

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
        _baseService?.Start();
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

        return _baseService.SetLogFileName(request.Value);
    }

    public override async Task ReadData(
        Empty request,
        IServerStreamWriter<Proto.Sample> responseStream,
        ServerCallContext context)
    {
        if (_myGaze == null || _baseService == null)
            return;

        _myGaze.Start();
        if (!_myGaze.IsTracking)
        {
            _logger.LogError("Failed to start tracking");
            return;
        }

        await _baseService.ReadData(request, responseStream, context, 2);

        _myGaze.Stop();
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
    readonly Tools.TelemetryService<Proto.Sample, Proto.Event>? _baseService;

    MyGaze? _myGaze;

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

        _baseService?.Publish(data);
    }

    #endregion
}