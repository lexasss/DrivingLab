using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;
using Channel = System.Threading.Channels.Channel;
using EyeXCore = Tobii.Gaze.Core;

namespace Server.TobiiEyeX;

internal class TobiiEyeXService : Gaze.Dispatcher.DispatcherBase, ITelemetryService
{
    public bool IsAvailable() => _eyeX != null;

    public TobiiEyeXService(ILogger<TobiiEyeXService> logger) : base()
    {
        _logger = logger;

        try
        {
            _eyeX = new EyeX(logger);

            if (_eyeX.IsValid)
            {
                _eyeX.Tracker?.GazeData += EyeX_GazeData;
                _eyeX.PosStream?.Next += EyeX_Pos;
                _eyeX.GazeStream?.Next += EyeX_Gaze;

                _logger.LogInformation("[EYEX] Service is running");
            }
            else
            {
                _eyeX = null;
                throw new Exception();
            }
        }
        catch (Exception)
        {
            _logger.LogCritical("[EYEX] Cannot start the service");
        }
    }

    public void Dispose()
    {
        _cts.Cancel();

        _eyeX?.Dispose();
        _eyeX = null;

        _logger.LogInformation("[EYEX] Service was disposed");
    }

    public override Task<Common.BoolReply> IsAvailable(Empty request, ServerCallContext context)
    {
        return Task.FromResult(new Common.BoolReply { Success = IsAvailable() });
    }

    public override Task<Empty> Start(Empty request, ServerCallContext context)
    {
        _isSending = true;
        return Task.FromResult(new Empty());
    }

    public override Task<Empty> Stop(Empty request, ServerCallContext context)
    {
        _isSending = false;
        return Task.FromResult(new Empty());
    }

    public override async Task ReadData(Empty request, IServerStreamWriter<Gaze.Sample> responseStream, ServerCallContext context)
    {
        if (_eyeX == null)
            return;

        _eyeX.Start();

        await foreach (var data in _channel.Reader.ReadAllAsync(_cts.Token))
        {
            if (_isSending)
                await responseStream.WriteAsync(data);
        }

        _eyeX.Stop();
    }


    #region Internal

    readonly static int SCREEN_WIDTH = GetSystemMetrics(SystemMetric.SM_CXSCREEN);
    readonly static int SCREEN_HEIGHT = GetSystemMetrics(SystemMetric.SM_CYSCREEN);

    readonly ILogger<TobiiEyeXService> _logger;
    readonly Channel<Gaze.Sample> _channel = Channel.CreateUnbounded<Gaze.Sample>();
    readonly CancellationTokenSource _cts = new();

    bool _isSending = false;

    EyeX? _eyeX;

    readonly Gaze.Sample _sample = new();

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

        Gaze.Sample.Types.Eye validEye = Gaze.Sample.Types.Eye.None;

        switch (e.GazeData.TrackingStatus)
        {
            case EyeXCore.TrackingStatus.BothEyesTracked:
                left = new EyeXCore.Point2D(e.GazeData.Left.GazePointOnDisplayNormalized.X, e.GazeData.Left.GazePointOnDisplayNormalized.Y);
                right = new EyeXCore.Point2D(e.GazeData.Right.GazePointOnDisplayNormalized.X, e.GazeData.Right.GazePointOnDisplayNormalized.Y);
                validEye = Gaze.Sample.Types.Eye.Both;
                x = (left.X + right.X) / 2;
                y = (left.Y + right.Y) / 2;
                break;

            case EyeXCore.TrackingStatus.OnlyLeftEyeTracked:
            case EyeXCore.TrackingStatus.OneEyeTrackedProbablyLeft:
            case EyeXCore.TrackingStatus.OneEyeTrackedUnknownWhich:
                left = new EyeXCore.Point2D(e.GazeData.Left.GazePointOnDisplayNormalized.X, e.GazeData.Left.GazePointOnDisplayNormalized.Y);
                right = new EyeXCore.Point2D(0.0, 0.0);
                validEye = Gaze.Sample.Types.Eye.Left;
                x = left.X;
                y = left.Y;
                break;

            case EyeXCore.TrackingStatus.OnlyRightEyeTracked:
            case EyeXCore.TrackingStatus.OneEyeTrackedProbablyRight:
                left = new EyeXCore.Point2D(0.0, 0.0);
                right = new EyeXCore.Point2D(e.GazeData.Right.GazePointOnDisplayNormalized.X, e.GazeData.Right.GazePointOnDisplayNormalized.Y);
                validEye = Gaze.Sample.Types.Eye.Right;
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

        _channel.Writer.TryWrite(_sample);
    }

    // WinAPI

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern int GetSystemMetrics(SystemMetric smIndex);

    enum SystemMetric
    {
        SM_CXSCREEN = 0,
        SM_CYSCREEN = 1,
    }

    #endregion
}