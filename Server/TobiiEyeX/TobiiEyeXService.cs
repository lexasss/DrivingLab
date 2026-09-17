using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using EyeXCore = Tobii.Gaze.Core;
using Proto = global::Gaze;

namespace Server.TobiiEyeX;

internal class TobiiEyeXService :
    Proto.Dispatcher.DispatcherBase,
    ITelemetryService
{
    public bool IsAvailable() => _eyeX != null;

    public TobiiEyeXService(ILoggerFactory loggerFactory) : base()
    {
        _logger = loggerFactory.CreateLogger("EYEX");

        try
        {
            _eyeX = new EyeX(_logger);

            if (_eyeX.IsValid)
            {
                _eyeX.Tracker?.GazeData += EyeX_GazeData;
                _eyeX.PosStream?.Next += EyeX_Pos;
                _eyeX.GazeStream?.Next += EyeX_Gaze;

                _baseService = new(_logger);
            }
            else
            {
                _eyeX.Dispose();
                _eyeX = null;

                throw new Exception();
            }
        }
        catch (Exception)
        {
            _logger.LogError("Cannot start the service");
        }
    }

    public void Dispose()
    {
        _eyeX?.Dispose();
        _baseService?.Dispose();

        _eyeX = null;

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
        if (_eyeX == null || _baseService == null)
            return;

        _eyeX.Tracker?.StartTracking();

        await _baseService.ReadData(request, responseStream, context, 3);

        _eyeX.Tracker?.StopTracking();
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

    readonly static int SCREEN_WIDTH = 
        GetSystemMetrics(SystemMetric.SM_CXSCREEN);
    readonly static int SCREEN_HEIGHT =
        GetSystemMetrics(SystemMetric.SM_CYSCREEN);

    readonly ILogger _logger;
    readonly Tools.TelemetryService<Proto.Sample, Proto.Event>? _baseService;
    readonly Proto.Sample _sample = new();

    EyeX? _eyeX;

    // Event handlers

    private void EyeX_Pos(object? sender, EyeXFramework.EyePositionEventArgs e)
    {
        lock (_sample)
        {
            _sample.CamXL = e.LeftEye.X;
            _sample.CamYL = e.LeftEye.Y;
            _sample.CamXR = e.RightEye.X;
            _sample.CamYR = e.RightEye.Y;
        };
    }

    private void EyeX_Gaze(object? sender, EyeXFramework.GazePointEventArgs e)
    {
        lock (_sample)
        {
            _sample.Timestamp = e.Timestamp;
            _sample.EyeX = e.X;
            _sample.EyeY = e.Y;
        };
    }

    private void EyeX_GazeData(object? sender, EyeXCore.GazeDataEventArgs e)
    {
        EyeXCore.Point2D left, right;
        double x = 0, y = 0;

        Proto.Sample.Types.Eye validEye = Proto.Sample.Types.Eye.None;

        switch (e.GazeData.TrackingStatus)
        {
            case EyeXCore.TrackingStatus.BothEyesTracked:
                left = new EyeXCore.Point2D(
                    e.GazeData.Left.GazePointOnDisplayNormalized.X,
                    e.GazeData.Left.GazePointOnDisplayNormalized.Y
                );
                right = new EyeXCore.Point2D(
                    e.GazeData.Right.GazePointOnDisplayNormalized.X,
                    e.GazeData.Right.GazePointOnDisplayNormalized.Y
                );
                validEye = Proto.Sample.Types.Eye.Both;
                x = (left.X + right.X) / 2;
                y = (left.Y + right.Y) / 2;
                break;

            case EyeXCore.TrackingStatus.OnlyLeftEyeTracked:
            case EyeXCore.TrackingStatus.OneEyeTrackedProbablyLeft:
            case EyeXCore.TrackingStatus.OneEyeTrackedUnknownWhich:
                left = new EyeXCore.Point2D(
                    e.GazeData.Left.GazePointOnDisplayNormalized.X,
                    e.GazeData.Left.GazePointOnDisplayNormalized.Y
                );
                right = new EyeXCore.Point2D(0.0, 0.0);
                validEye = Proto.Sample.Types.Eye.Left;
                x = left.X;
                y = left.Y;
                break;

            case EyeXCore.TrackingStatus.OnlyRightEyeTracked:
            case EyeXCore.TrackingStatus.OneEyeTrackedProbablyRight:
                left = new EyeXCore.Point2D(0.0, 0.0);
                right = new EyeXCore.Point2D(
                    e.GazeData.Right.GazePointOnDisplayNormalized.X,
                    e.GazeData.Right.GazePointOnDisplayNormalized.Y
                );
                validEye = Proto.Sample.Types.Eye.Right;
                x = right.X;
                y = right.Y;
                break;

            default:
                left = right = new EyeXCore.Point2D(0.0, 0.0);
                break;
        }

        lock (_sample)
        {
            _sample.ValidEye = validEye;
            _sample.EyeXL = left.X * SCREEN_WIDTH;
            _sample.EyeYL = left.Y * SCREEN_HEIGHT;
            _sample.EyeXR = right.X * SCREEN_WIDTH;
            _sample.EyeYR = right.Y * SCREEN_HEIGHT;
            if (_sample.EyeX == 0)
            {
                _sample.EyeX = x * SCREEN_WIDTH;
                _sample.EyeY = y * SCREEN_HEIGHT;
            }
        }

        _baseService?.Publish(_sample);
    }

    #endregion

    #region WinAPI

    enum SystemMetric
    {
        SM_CXSCREEN = 0,
        SM_CYSCREEN = 1,
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern int GetSystemMetrics(SystemMetric smIndex);

    #endregion
}