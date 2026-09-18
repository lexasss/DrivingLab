using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ClientExample;

public partial class ButtonState : ObservableObject
{
    [ObservableProperty]
    public partial bool IsPressed { get; set; } = false;
}

public partial class SliderState : ObservableObject
{
    [ObservableProperty]
    public partial double Value { get; set; } = 0;
}

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

    public ObservableCollection<ButtonState> Buttons { get; } =
        new(Enumerable.Range(0, 18).Select(_ => new ButtonState()));
    public ObservableCollection<SliderState> Sliders { get; } =
        new(Enumerable.Range(0, 2).Select(_ => new SliderState()));
    [ObservableProperty]
    public partial double PointOfView { get; set; } = double.NaN;
    [ObservableProperty]
    public partial System.Windows.Point Point { get; set; } = new System.Windows.Point(0, 0);

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
            if (!IsConnected)
            {
                IsStreaming = false;

                System.Windows.Application.Current.Dispatcher.Invoke(async () => {
                    Device = null;
                    await Task.Delay(500);
                    UpdateDeviceList();
                });
            }
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
        foreach (var device in _pointingClient.GetDevices(Pointing.DeviceType.Mouse))
            Devices.Add(device);
        foreach (var device in _pointingClient.GetDevices(Pointing.DeviceType.Joystick))
            Devices.Add(device);
        foreach (var device in _pointingClient.GetDevices(Pointing.DeviceType.Gamepad))
            Devices.Add(device);
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

    private void SetData(Pointing.Data data)
    {
        Data = $"Z = {data.Point.Z:F3} | {data.Rotation.Z:F3}";

        Point = new System.Windows.Point(data.Point.X, data.Point.Y);

        foreach (var button in data.Buttons)
            if (button.Id < Buttons.Count)
                Buttons[button.Id].IsPressed = button.IsPressed;
        foreach (var slider in data.Sliders)
            if (slider.Type == Pointing.SliderType.General)
                Sliders[slider.Id].Value = slider.Value;
        foreach (var pointOfView in data.PointOfViews)
            if (pointOfView.Id == 0)
                PointOfView = pointOfView.IsPressed ? pointOfView.Degrees : double.NaN;
    }

    #endregion
}
