using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ClientExample;

public partial class PointingViewModel : ObservableObject
{
    public bool IsAvailable => _pointingClient.IsAvailable;
    public ObservableCollection<Pointing.Device> Devices { get; } = [];
    
    [ObservableProperty]
    public partial bool IsConnected { get; set; } = false;
    [ObservableProperty]
    public partial Pointing.Device? Device { get; set; } = null;
    [ObservableProperty]
    public partial string Data { get; set; } = string.Empty;
    [ObservableProperty]
    public partial bool IsStreaming { get; set; } = false;
    [ObservableProperty]
    public partial bool IsLogging { get; set; } = false;

    public PointingViewModel(PointingClient pointingClient)
    {
        _pointingClient = pointingClient;
        _pointingClient.AvailabilityChanged += (s, e) =>
        {
            IsConnected = _pointingClient.IsConnected;
            IsStreaming = _pointingClient.IsReading;
            OnPropertyChanged(nameof(IsAvailable));

            if (IsAvailable)
                UpdateDeviceList();
        };
        _pointingClient.ConnectionChanged += (s, e) =>
        {
            IsConnected = e;
        };
        _pointingClient.DataUpdated += (s, e) =>
        {
            if (_pointingClient.IsReading)
                SetData(e);
        };
    }

    #region Internal

    readonly PointingClient _pointingClient;

    [RelayCommand]
    private void UpdateDeviceList()
    {
        Devices.Clear();
        foreach (var device in _pointingClient.GetDevices(Pointing.DeviceType.Joystick))
        {
            Devices.Add(device);
        }
    }

    partial void OnDeviceChanged(Pointing.Device? value)
    {
        _pointingClient.SetPointingDevice(value);
    }

    partial void OnIsStreamingChanged(bool value)
    {
        if (_pointingClient.IsReading)
        {
            _pointingClient.Stop();
            Data = string.Empty;
        }
        else
        {
            _pointingClient.Start();
            IsStreaming = _pointingClient.IsReading;
        }
    }

    partial void OnIsLoggingChanged(bool value)
    {
        _pointingClient.SetLoggingEnabled(value);
        IsLogging = _pointingClient.IsLogging;
    }

    private void SetData(Pointing.Data data) =>
        Data = $"X = {data.Point.X:F3}\nY = {data.Point.Y:F3}\nZ = {data.Rotation.Z:F3}";

    #endregion
}
