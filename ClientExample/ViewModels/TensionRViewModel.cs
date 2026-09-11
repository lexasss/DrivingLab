using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ClientExample;

public partial class TensionRViewModel : ObservableObject
{
    public bool IsAvailable => _tensionRClient.IsAvailable;
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
        _tensionRClient = tensionRClient;

        IsConnected = _tensionRClient.IsConnected;
        IsCalibrated = _tensionRClient.IsCalibrated;
        IsEnabled = _tensionRClient.IsEnabled;

        _tensionRClient.AvailabilityChanged += (s, e) =>
        {
            OnPropertyChanged(nameof(IsAvailable));
        };

        _tensionRClient.ConnectionChanged += (s, e) =>
        {
            IsConnecting = false;
            IsConnected = e;
            if (!IsConnected)
            {
                IsCalibrated = false;
                IsEnabled = false;
            }
        };
        _tensionRClient.CalibrationChanged += (s, e) =>
        {
            IsCalibrating = false;
            IsCalibrated = e;
        };
        _tensionRClient.EnabledChanged += (s, e) => IsEnabled = e;

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

    readonly TensionRClient _tensionRClient;
    readonly COMUtils _comUtils = new();

    [RelayCommand]
    private void Connect()
    {
        if (ComPort != null)
        {
            IsConnecting = true;
            _tensionRClient.Connect(ComPort.Name);
        }
    }

    [RelayCommand]
    private void Calibrate()
    {
        IsCalibrating = true;
        _tensionRClient.Calibrate();
    }

    partial void OnIsEnabledChanged(bool value)
    {
        if (value)
            _tensionRClient.Start();
        else
            _tensionRClient.Stop();
    }

    partial void OnIsLoggingChanged(bool value)
    {
        _tensionRClient.SetLoggingEnabled(value);
    }

    partial void OnTensionChanged(int value)
    {
        _tensionRClient.SetTension(value, Side);
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
