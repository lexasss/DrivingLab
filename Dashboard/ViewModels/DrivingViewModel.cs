using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using Dashboard.Tools;

namespace Dashboard;

public partial class DrivingViewModel : ObservableObject
{
    public bool IsAvailable => _client.IsAvailable;
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAnyDeviceConnected))]
    public partial bool IsBaseConnected { get; set; } = false;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAnyDeviceConnected))]
    public partial bool IsWheelConnected { get; set; } = false;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAnyDeviceConnected))]
    public partial bool ArePedalsConnected { get; set; } = false;
    [ObservableProperty]
    public partial bool IsActivePedalsHubConnected { get; set; } = false;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAnyDeviceConnected))]
    public partial bool IsActiveBrakeConnected { get; set; } = false;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAnyDeviceConnected))]
    public partial bool IsActiveThrottleConnected { get; set; } = false;
    public bool IsAnyDeviceConnected => 
        IsBaseConnected || 
        IsWheelConnected || 
        ArePedalsConnected || 
        IsActiveBrakeConnected || 
        IsActiveThrottleConnected;
    [ObservableProperty]
    public partial bool IsPlayingEffect { get; set; } = false;
    [ObservableProperty]
    public partial bool IsStreaming { get; set; } = false;
    [ObservableProperty]
    public partial bool IsLogging { get; set; } = false;
    [ObservableProperty]
    public partial Driving.EffectType EffectType { get; set; } = Driving.EffectType.Periodic;
    [ObservableProperty]
    public partial Driving.EffectVariable EffectVariable { get; set; } =
        Driving.EffectVariable.ForceN;
    [ObservableProperty]
    public partial float EffectAmplitude { get; set; } = 1;
    [ObservableProperty]
    public partial int EffectDuration { get; set; } = 1000; // ms

    [ObservableProperty]
    public partial Driving.PeriodicEffectType PeriodicEffectType { get ; set; } = 
        Driving.PeriodicEffectType.Sine;
    [ObservableProperty]
    public partial float PeriodicEffectFrequency { get; set; } = 20;

    public ObservableCollection<ButtonState> Buttons { get; } =
        new(Enumerable.Range(0, 14).Select(_ => new ButtonState()));
    public ObservableCollection<SliderState> Sliders { get; } =
        new(Enumerable.Range(0, 7).Select(_ => new SliderState()));

    [ObservableProperty]
    public partial double PointOfView { get; set; } = double.NaN;

    public DrivingViewModel(GrpcClients.DrivingClient drivingClient)
    {
        _client = drivingClient;
        _client.AvailabilityChanged += (s, e) =>
        {
            IsBaseConnected = _client.IsBaseConnected;
            IsWheelConnected = _client.IsWheelConnected;
            ArePedalsConnected = _client.ArePedalsConnected;
            IsActivePedalsHubConnected = _client.IsActivePedalsHubConnected;
            IsActiveBrakeConnected = _client.IsActiveBrakePedalConnected;
            IsActiveThrottleConnected = _client.IsActiveThrottlePedalConnected;

            IsStreaming = _client.IsStreamingData;
            IsLogging = _client.IsLogging;

            OnPropertyChanged(nameof(IsAvailable));
        };
        _client.BaseConnectionChanged += (s, e) =>
        {
            IsBaseConnected = e;
        };
        _client.WheelConnectionChanged += (s, e) =>
        {
            IsWheelConnected = e;
        };
        _client.PedalsConnectionChanged += (s, e) =>
        {
            ArePedalsConnected = e;
        };
        _client.ActivePedalsHubConnectionChanged += (s, e) =>
        {
            IsActivePedalsHubConnected = e;
        };
        _client.ActiveBrakeConnectionChanged += (s, e) =>
        {
            IsActiveBrakeConnected = e;
        };
        _client.ActiveThrottleConnectionChanged += (s, e) =>
        {
            IsActiveThrottleConnected = e;
        };
        _client.EffectFinished += (s, e) =>
        {
            IsPlayingEffect = false;
        };
        _client.PeriodicEffectParametersRetrieved += (s, e) =>
        {
            PeriodicEffectType = e.Type;
            PeriodicEffectFrequency = e.Frequency;
        };
        _client.DataUpdated += (s, e) =>
        {
            if (_client.IsStreamingData)
                SetData(e);
        };
    }

    #region Internal

    readonly GrpcClients.DrivingClient _client;

    [RelayCommand]
    private void PlayBrakeEffect()
    {
        IsPlayingEffect = true;
        _client.PlayPedalEffect(new()
        {
            Pedal = Driving.SimucubeDevice.BrakePedal,
            Type = EffectType,
            Variable = EffectVariable,
            Amplitude = EffectAmplitude,
            Duration = EffectDuration
        });
    }

    [RelayCommand]
    private void PlayThrottleEffect()
    {
        IsPlayingEffect = true;
        _client.PlayPedalEffect(new()
        {
            Pedal = Driving.SimucubeDevice.ThrottlePedal,
            Type = EffectType,
            Variable = EffectVariable,
            Amplitude = EffectAmplitude,
            Duration = EffectDuration
        });
    }

    partial void OnIsStreamingChanging(bool oldValue, bool newValue)
    {
        if (oldValue)
        {
            _client.Stop();
        }
        else
        {
            _client.Start();
        }
    }

    partial void OnIsLoggingChanged(bool value)
    {
        _client.SetLoggingEnabled(value);
        IsLogging = _client.IsLogging;
    }

    partial void OnPeriodicEffectTypeChanged(Driving.PeriodicEffectType value)
    {
        _client.SetPeriodicEffectParameters(new()
        {
            Type = value,
            Frequency = PeriodicEffectFrequency
        });
    }

    partial void OnPeriodicEffectFrequencyChanged(float value)
    {
        _client.SetPeriodicEffectParameters(new()
        {
            Type = PeriodicEffectType,
            Frequency = value
        });
    }

    private void SetData(Driving.Data data)
    {
        Buttons[0].IsPressed = data.TopLeftButton1.IsPressed;
        Buttons[1].IsPressed = data.TopLeftButton2.IsPressed;
        Buttons[2].IsPressed = data.TopLeftButton3.IsPressed;
        Buttons[3].IsPressed = data.TopRightButton1.IsPressed;
        Buttons[4].IsPressed = data.TopRightButton2.IsPressed;
        Buttons[5].IsPressed = data.TopRightButton3.IsPressed;
        Buttons[6].IsPressed = data.BottomLeftButton1.IsPressed;
        Buttons[7].IsPressed = data.BottomLeftButton2.IsPressed;
        Buttons[8].IsPressed = data.RotaryButton.IsPressed;
        Buttons[9].IsPressed = data.BottomRightButton1.IsPressed;
        Buttons[10].IsPressed = data.BottomRightButton2.IsPressed;
        Buttons[11].IsPressed = data.RotaryTiltButton.IsPressed;
        Buttons[12].IsPressed = data.LeftPaddleShifter.IsPressed;
        Buttons[13].IsPressed = data.RightPaddleShifter.IsPressed;

        Sliders[0].Value = data.WheelRotation;
        Sliders[1].Value = data.RotaryButton.RotationValue.Wrap(-1, 1);
        Sliders[2].Value = data.RotaryTiltButton.RotationValue.Wrap(-1, 1);
        Sliders[3].Value = data.BrakePedal;
        Sliders[4].Value = data.ThrottlePedal;
        Sliders[5].Value = data.ActiveBrakePedal;
        Sliders[6].Value = data.ActiveThrottlePedal;

        PointOfView = data.RotaryTiltButton.IsTilted
            ? data.RotaryTiltButton.Degrees
            : double.NaN;
    }

    #endregion
}
