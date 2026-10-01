using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace Dashboard;

public partial class TensionRViewModel : ObservableObject
{
    public bool IsAvailable => _client.IsAvailable;
    [ObservableProperty]
    public partial bool IsConnecting { get; set; } = false;
    [ObservableProperty]
    public partial bool IsConnected { get; set; } = false;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CalibrateButtonText))]
    public partial bool IsCalibrating { get; set; } = false;
    [ObservableProperty]
    public partial bool IsCalibrated { get; set; } = false;
    [ObservableProperty]
    public partial bool IsEnabled { get; set; } = false;
    [ObservableProperty]
    public partial string Data { get; set; } = string.Empty;
    [ObservableProperty]
    public partial bool IsLogging { get; set; } = false;
    [ObservableProperty]
    public partial int Tension { get; set; } = 0;
    [ObservableProperty]
    public partial TensionR.Side Side { get; set; } = TensionR.Side.Both;
    [ObservableProperty]
    public partial COMUtils.Port? ComPort { get; set; } = null;

    public string CalibrateButtonText => IsCalibrating ? "Calibrating" : "Calibrate";
    public ObservableCollection<COMUtils.Port> ComPorts { get; } = [];

    public TensionRViewModel(TensionRClient tensionRClient)
    {
        _client = tensionRClient;

        _client.AvailabilityChanged += (s, e) =>
        {
            IsConnected = _client.IsConnected;
            IsCalibrated = _client.IsCalibrated;
            IsEnabled = _client.IsEnabled;
            IsLogging = _client.IsLogging;

            OnPropertyChanged(nameof(IsAvailable));
        };

        _client.ConnectionChanged += (s, e) =>
        {
            IsConnecting = false;
            IsConnected = e;
            if (!IsConnected)
            {
                IsCalibrated = false;
                IsEnabled = false;
            }
        };
        _client.CalibrationChanged += (s, e) =>
        {
            IsCalibrating = false;
            IsCalibrated = e;
        };
        _client.EnabledChanged += (s, e) => IsEnabled = e;

        _comUtils.Inserted += (s, e) =>
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                ComPorts.Add(e);

                if (!IsConnected && ComPort == null)
                {
                    SetDefaulPort();
                }
            });
        };
        _comUtils.Removed += (s, e) =>
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                for (int i = 0; i < ComPorts.Count; i++)
                    if (ComPorts[i].Name == e.Name)
                        ComPorts.RemoveAt(i);
            });
        };

        foreach (var port in _comUtils.Ports)
            ComPorts.Add(port);

        Task.Run(async () =>
        {
            await Task.Delay(500);
            SetDefaulPort();
        });
    }

    #region Internal

    const string TENSIONR_PORT_DESCRIPTION = "Silicon Labs CP210x USB to UART Bridge";

    readonly TensionRClient _client;
    readonly COMUtils _comUtils = new();

    [RelayCommand]
    private void Connect()
    {
        if (ComPort != null)
        {
            IsConnecting = true;
            _client.Connect(ComPort.Name);
        }
    }

    [RelayCommand]
    private void Calibrate()
    {
        IsCalibrating = true;
        _client.Calibrate();
    }

    partial void OnIsEnabledChanged(bool value)
    {
        if (value)
            _client.Start();
        else
            _client.Stop();
    }

    partial void OnIsLoggingChanged(bool value)
    {
        _client.SetLoggingEnabled(value);
    }

    partial void OnTensionChanged(int value)
    {
        _client.SetTension(value, Side);
    }

    partial void OnComPortChanged(COMUtils.Port? value)
    {
        if (value == null && IsConnected)
        {
            IsConnected = false;
            IsCalibrating = false;
            IsCalibrated = false;
            IsEnabled = false;
        }
    }

    private void SetDefaulPort()
    {
        ComPort = ComPorts.FirstOrDefault(port => port.Description?.Contains(TENSIONR_PORT_DESCRIPTION) ?? false);
    }

    #endregion
}
