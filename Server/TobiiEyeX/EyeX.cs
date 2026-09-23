using Microsoft.Extensions.Logging;
using EyeXCore = Tobii.Gaze.Core;

namespace Server.TobiiEyeX;

internal class EyeX : IDisposable
{
    public event EventHandler? StatusChanged;

    public EyeXCore.EyeTracker? Tracker => _tracker;
    public EyeXFramework.EyePositionDataStream? PosStream => _posStream;
    public EyeXFramework.GazePointDataStream? GazeStream => _gazeStream;

    public bool IsValid { get; private set; } = false;
    public bool IsConnected => 
        _status != Tobii.EyeX.Framework.EyeTrackingDeviceStatus.ConnectionError &&
        _status != Tobii.EyeX.Framework.EyeTrackingDeviceStatus.DeviceNotConnected &&
        _status != Tobii.EyeX.Framework.EyeTrackingDeviceStatus.Initializing &&
        _status != Tobii.EyeX.Framework.EyeTrackingDeviceStatus.NotAvailable &&
        _status != Tobii.EyeX.Framework.EyeTrackingDeviceStatus.TrackingUnavailable &&
        _status != Tobii.EyeX.Framework.EyeTrackingDeviceStatus.UnknownError;
    public bool IsCalibrating =>
        _status == Tobii.EyeX.Framework.EyeTrackingDeviceStatus.Configuring;
    public bool IsCalibrated =>
        _status == Tobii.EyeX.Framework.EyeTrackingDeviceStatus.Tracking ||
        _status == Tobii.EyeX.Framework.EyeTrackingDeviceStatus.TrackingPaused ||
        _status == Tobii.EyeX.Framework.EyeTrackingDeviceStatus.Configuring;
    public bool IsTracking =>
        _status == Tobii.EyeX.Framework.EyeTrackingDeviceStatus.Tracking;

    public EyeX(ILogger logger)
    {
        _logger = logger;

        _host = new EyeXFramework.EyeXHost();
        _host.EyeTrackingDeviceStatusChanged += Host_DeviceStatusChanged;
        _host.Start();

        using var etLib = new EyeXCore.EyeTrackerCoreLibrary();

        Uri url = etLib.GetConnectedEyeTracker();
        if (url == null)
        {
            _logger.LogWarning("No devices");
            return;
        }

        try
        {
            _tracker = new EyeXCore.EyeTracker(url);
        }
        catch (EyeXCore.EyeTrackerException ex)
        {
            _logger.LogError("Failed to created an eye tracker instance on {url} ({msg})",
                url,
                ex.Message);
            return;
        }

        _tracker.RunEventLoopOnInternalThread(error => {
            if (error != EyeXCore.ErrorCode.Success)
                _logger.LogError("Tobii internal error: {error}", error);
            else
                _logger.LogInformation("Tobii internal operation succeeded");
        });

        _tracker.ConnectAsync(error =>
        {
            if (error == EyeXCore.ErrorCode.Success)
                _logger.LogInformation("Connected");
            else
                _logger.LogError("Cannot connect to the device ({error})", error);
        });

        _posStream = _host.CreateEyePositionDataStream();

        _gazeStream = _host.CreateGazePointDataStream(
            Tobii.EyeX.Framework.GazePointDataMode.Unfiltered
        );

        IsValid = true;
    }

    public void Dispose()
    {
        _posStream?.Dispose();
        _gazeStream?.Dispose();
        _tracker?.Dispose();
        _host?.Dispose();
    }

    #region Internal

    readonly EyeXFramework.EyeXHost _host;
    readonly EyeXCore.EyeTracker? _tracker;
    readonly EyeXFramework.EyePositionDataStream? _posStream;
    readonly EyeXFramework.GazePointDataStream? _gazeStream;
    readonly ILogger _logger;

    Tobii.EyeX.Framework.EyeTrackingDeviceStatus _status = Tobii.EyeX.Framework.EyeTrackingDeviceStatus.NotAvailable;

    private void Host_DeviceStatusChanged(
        object? sender,
        EyeXFramework.EngineStateValue<Tobii.EyeX.Framework.EyeTrackingDeviceStatus> e)
    {
        if (e.IsValid)
        {
            _logger.LogInformation("Status: {status}", e.Value);

            var prevStatus = _status;
            _status = e.Value;

            StatusChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    #endregion
}
