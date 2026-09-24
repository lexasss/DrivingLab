using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ClientExample;

public partial class DrivingViewModel : ObservableObject
{
    public bool IsAvailable => _drivingClient.IsAvailable;
    
    [ObservableProperty]
    public partial bool IsBaseConnected { get; set; } = false;
    [ObservableProperty]
    public partial bool IsWheelConnected { get; set; } = false;
    [ObservableProperty]
    public partial bool ArePedalsConnected { get; set; } = false;
    [ObservableProperty]
    public partial bool IsActivePedalsHubConnected { get; set; } = false;
    [ObservableProperty]
    public partial bool IsActiveBrakeConnected { get; set; } = false;
    [ObservableProperty]
    public partial bool IsActiveThrottleConnected { get; set; } = false;
    [ObservableProperty]
    public partial bool IsPlayingEffect { get; set; } = false;
    [ObservableProperty]
    public partial bool IsStreaming { get; set; } = false;
    [ObservableProperty]
    public partial bool IsLogging { get; set; } = false;
    [ObservableProperty]
    public partial Driving.EffectType EffectType { get; set; } = Driving.EffectType.Periodic;
    [ObservableProperty]
    public partial Driving.EffectVariable EffectVariable { get; set; } = Driving.EffectVariable.ForceN;
    [ObservableProperty]
    public partial float EffectAmplitude { get; set; } = 1;
    [ObservableProperty]
    public partial int EffectDuration { get; set; } = 1000; // ms

    [ObservableProperty]
    public partial Driving.PeriodicEffectType PeriodicEffectType { get ; set; } = Driving.PeriodicEffectType.Sine;
    [ObservableProperty]
    public partial float PeriodicEffectFrequency { get; set; } = 20;

    public ObservableCollection<ButtonState> Buttons { get; } =
        new(Enumerable.Range(0, 14).Select(_ => new ButtonState()));
    public ObservableCollection<SliderState> Sliders { get; } =
        new(Enumerable.Range(0, 7).Select(_ => new SliderState()));

    [ObservableProperty]
    public partial double PointOfView { get; set; } = double.NaN;

    public DrivingViewModel(DrivingClient drivingClient)
    {
        _drivingClient = drivingClient;
        _drivingClient.AvailabilityChanged += (s, e) =>
        {
            IsBaseConnected = _drivingClient.IsBaseConnected;
            IsWheelConnected = _drivingClient.IsWheelConnected;
            ArePedalsConnected = _drivingClient.ArePedalsConnected;
            IsActivePedalsHubConnected = _drivingClient.IsActivePedalsHubConnected;
            IsActiveBrakeConnected = _drivingClient.ActivePedalsConnected.HasFlag(Driving.ActivePedal.Brake);
            IsActiveThrottleConnected = _drivingClient.ActivePedalsConnected.HasFlag(Driving.ActivePedal.Throttle);

            OnPropertyChanged(nameof(IsAvailable));
        };
        _drivingClient.BaseConnectionChanged += (s, e) =>
        {
            IsBaseConnected = e;
        };
        _drivingClient.WheelConnectionChanged += (s, e) =>
        {
            IsWheelConnected = e;
        };
        _drivingClient.PedalsConnectionChanged += (s, e) =>
        {
            ArePedalsConnected = e;
        };
        _drivingClient.ActivePedalsHubConnectionChanged += (s, e) =>
        {
            IsActivePedalsHubConnected = e;
        };
        _drivingClient.ActivePedalsConnectionChanged += (s, e) =>
        {
            IsActiveBrakeConnected = e.HasFlag(Driving.ActivePedal.Brake);
            IsActiveThrottleConnected = e.HasFlag(Driving.ActivePedal.Throttle);
        };
        _drivingClient.EffectFinished += (s, e) =>
        {
            IsPlayingEffect = false;
        };
        _drivingClient.PeriodicEffectParametersRetrieved += (s, e) =>
        {
            PeriodicEffectType = e.Type;
            PeriodicEffectFrequency = e.Frequency;
        };
        _drivingClient.DataUpdated += (s, e) =>
        {
            if (_drivingClient.IsReading)
                SetData(e);
        };
    }

    #region Internal

    readonly DrivingClient _drivingClient;

    [RelayCommand]
    private void PlayBrakeEffect()
    {
        IsPlayingEffect = true;
        _drivingClient.PlayPedalEffect(new()
        {
            Pedal = Driving.ActivePedal.Brake,
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
        _drivingClient.PlayPedalEffect(new()
        {
            Pedal = Driving.ActivePedal.Throttle,
            Type = EffectType,
            Variable = EffectVariable,
            Amplitude = EffectAmplitude,
            Duration = EffectDuration
        });
    }

    partial void OnIsStreamingChanged(bool value)
    {
        if (_drivingClient.IsReading)
        {
            _drivingClient.Stop();
        }
        else
        {
            _drivingClient.Start();
            IsStreaming = _drivingClient.IsReading;
        }
    }

    partial void OnIsLoggingChanged(bool value)
    {
        _drivingClient.SetLoggingEnabled(value);
        IsLogging = _drivingClient.IsLogging;
    }

    partial void OnPeriodicEffectTypeChanged(Driving.PeriodicEffectType value)
    {
        _drivingClient.SetPeriodicEffectParameters(new()
        {
            Type = value,
            Frequency = PeriodicEffectFrequency
        });
    }

    partial void OnPeriodicEffectFrequencyChanged(float value)
    {
        _drivingClient.SetPeriodicEffectParameters(new()
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
        Sliders[1].Value = data.RotaryButton.Rotation;
        Sliders[2].Value = data.RotaryTiltButton.Rotation;
        Sliders[3].Value = data.BrakePedal;
        Sliders[4].Value = data.ThrottlePedal;
        Sliders[5].Value = data.ActiveBrakePedal;
        Sliders[6].Value = data.ActiveThrottlePedal;

        PointOfView = data.RotaryTiltButton.IsTilted ? data.RotaryTiltButton.Degrees : double.NaN;
    }

    #endregion
}
