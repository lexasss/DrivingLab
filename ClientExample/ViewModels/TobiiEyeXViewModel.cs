using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows;

namespace ClientExample;

public partial class TobiiEyeXViewModel : ObservableObject
{
    public bool IsAvailable => _client.IsAvailable;
    [ObservableProperty]
    public partial bool IsConnected { get; set; } = false;
    [ObservableProperty]
    public partial bool IsCalibrating { get; set; } = false;
    [ObservableProperty]
    public partial bool IsCalibrated { get; set; } = false;
    [ObservableProperty]
    public partial bool IsTracking { get; set; } = false;
    [ObservableProperty]
    public partial bool IsStreaming { get; set; } = false;
    [ObservableProperty]
    public partial bool IsLogging { get; set; } = false;
    [ObservableProperty]
    public partial string Data { get; set; } = string.Empty;

    public TobiiEyeXViewModel(TobiiEyeXClient client)
    {
        _client = client;
        _client.AvailabilityChanged += (s, e) =>
        {
            IsConnected = _client.IsConnected;
            IsCalibrating = _client.IsCalibrating;
            IsCalibrated = _client.IsCalibrated;
            IsTracking = _client.IsTracking;
            IsStreaming = _client.IsReading;
            OnPropertyChanged(nameof(IsAvailable));
        };

        _client.ConnectionStatusChanged += (s, e) => Application.Current.Dispatcher.Invoke(() => IsConnected = e);
        _client.CalibrationStageChanged += (s, e) => Application.Current.Dispatcher.Invoke(() => IsCalibrating = e);
        _client.CalibrationStatusChanged += (s, e) => Application.Current.Dispatcher.Invoke(() => IsCalibrated = e);
        _client.TrackingStatusChanged += (s, e) => Application.Current.Dispatcher.Invoke(() => IsTracking = e);
        _client.Sample += (s, e) =>
        {
            Data = $"X={e.EyeX:F1} Y={e.EyeY:F1}";
        };
    }

    #region Internal

    const string WAITING_GAZE = "waiting for gaze data...";

    readonly TobiiEyeXClient _client;

    partial void OnIsStreamingChanged(bool value)
    {
        if (_client.IsReading)
        {
            _client.Stop();
            Data = string.Empty;
        }
        else
        {
            _client.Start();
            Data = WAITING_GAZE;
        }
    }

    partial void OnIsLoggingChanged(bool value)
    {
        _client.SetLoggingEnabled(value);
    }

    private void ResetData() =>
        Data = WAITING_GAZE;

    #endregion
}
