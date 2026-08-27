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
                _logger.LogInformation("[EYEX] devices:");
                foreach (EyeXCore.DeviceInfo device in devices)
                {
                    _logger.LogInformation("[EYEX]   - " + device.ToString());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("[EYEX] failed to list Tobii EyeX devices: " + ex.Message);
            }

            Uri url = etLib.GetConnectedEyeTracker();
            if (url == null)
            {
                _logger.LogInformation("[EYEX] no devices");
                return;
            }

            try
            {
                _tracker = new EyeXCore.EyeTracker(url);
                _logger.LogInformation($"[EYEX] tracker initialized on {url}");
            }
            catch (EyeXCore.EyeTrackerException ex)
            {
                _logger.LogError("[EYEX] failed to created an eye tracker instance: " + ex.Message);
                return;
            }

            _tracker.RunEventLoopOnInternalThread((err) => { });
            _tracker.ConnectAsync((err) => _logger.LogInformation($"[EYEX] connection result: {err}"));
        }

        _posStream = _host.CreateEyePositionDataStream();

        _gazeStream = _host.CreateGazePointDataStream(EyeXFramewor.GazePointDataMode.Unfiltered);

        IsValid = true;
    }

    public void Start()
    {
        _tracker?.StartTrackingAsync((err) => _logger.LogInformation($"[EYEX] starting streaming: {err}"));
    }

    public void Stop()
    {
        _tracker?.StopTrackingAsync((err) => _logger.LogInformation($"[EYEX] stopping streaming: {err}"));
    }

    public void Dispose()
    {
        _posStream?.Dispose();
        _gazeStream?.Dispose();
        _tracker?.Dispose();
        _host?.Dispose();
    }

    // Internal

    readonly EyeXFramework.EyeXHost _host;
    readonly EyeXCore.EyeTracker? _tracker;
    readonly EyeXFramework.EyePositionDataStream? _posStream;
    readonly EyeXFramework.GazePointDataStream? _gazeStream;
    readonly ILogger<TobiiEyeXService> _logger;

    private void Host_DeviceStatusChanged(object? sender, EyeXFramework.EngineStateValue<EyeXFramewor.EyeTrackingDeviceStatus> e)
    {
        if (e.IsValid)
        {
            _logger.LogInformation($"[EYEX] status: {e.Value}");
        }
    }
}
