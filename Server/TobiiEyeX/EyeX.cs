using Microsoft.Extensions.Logging;
using EyeXCore = Tobii.Gaze.Core;
using EyeXFramewor = Tobii.EyeX.Framework;

namespace Server.TobiiEyeX;

internal class EyeX : IDisposable
{
    public EyeXCore.EyeTracker? Tracker => _tracker;
    public EyeXFramework.EyePositionDataStream? PosStream => _posStream;
    public EyeXFramework.GazePointDataStream? GazeStream =>_gazeStream;

    public bool IsValid { get; private set; } = false;

    public EyeX(ILogger<TobiiEyeXService> logger)
    {
        _logger = logger;

        _host = new EyeXFramework.EyeXHost();
        _host.EyeTrackingDeviceStatusChanged += Host_DeviceStatusChanged;
        _host.Start();

        using (var etLib = new EyeXCore.EyeTrackerCoreLibrary())
        {
            try
            {
                var devices = etLib.ListUsbEyeTrackers();
                _logger.LogInformation("[EYEX] Devices:");
                foreach (EyeXCore.DeviceInfo device in devices)
                {
                    _logger.LogInformation("[EYEX]   - " + device.ToString());
                }
            }
            catch (Exception)
            {
                _logger.LogError("[EYEX] Failed to list devices (Tobii EyeX software is not installed or not running)");
            }

            Uri url = etLib.GetConnectedEyeTracker();
            if (url == null)
            {
                _logger.LogInformation("[EYEX] No devices");
                return;
            }

            try
            {
                _tracker = new EyeXCore.EyeTracker(url);
            }
            catch (EyeXCore.EyeTrackerException ex)
            {
                _logger.LogError("[EYEX] Failed to created an eye tracker instance on {url} ({msg})", url, ex.Message);
                return;
            }

            _tracker.RunEventLoopOnInternalThread((error) => { });
            _tracker.ConnectAsync((error) =>
            {
                if (error == EyeXCore.ErrorCode.Success)
                    _logger.LogInformation("[EYEX] Connected");
                else
                    _logger.LogError("[EYEX] Cannot connect to the device ({error})", error);
            });
        }

        _posStream = _host.CreateEyePositionDataStream();

        _gazeStream = _host.CreateGazePointDataStream(EyeXFramewor.GazePointDataMode.Unfiltered);

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
    readonly ILogger<TobiiEyeXService> _logger;

    private void Host_DeviceStatusChanged(object? sender, EyeXFramework.EngineStateValue<EyeXFramewor.EyeTrackingDeviceStatus> e)
    {
        if (e.IsValid)
        {
            _logger.LogInformation($"[EYEX] Status: {e.Value}");
        }
    }

    #endregion
}
