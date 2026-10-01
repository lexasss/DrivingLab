using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dashboard.Tools;
using Tools;

namespace Dashboard;

public partial class CameraViewModel : ObservableObject
{
    public record class CameraStream(Camera.Stream Ref, string Description);

    public bool IsAvailable => _client.IsAvailable;
    public bool IsReadyToRecord => _isCameraStreamReady && _isVideoFileNameSet;
    [ObservableProperty]
    public partial bool IsRecording { get; set; } = false;
    [ObservableProperty]
    public partial string VideoFileName { get; set; } = string.Empty;
    [ObservableProperty]
    public partial Camera.Camera? Camera { get; set; } = null;
    [ObservableProperty]
    public partial CameraStream? Stream { get; set; } = null;
    [ObservableProperty]
    public partial string Data { get; set; } = string.Empty;

    [ObservableProperty]
    public partial double Exposure { get; set; } = 0;
    [ObservableProperty]
    public partial double ExposureMin { get; set; } = 0;
    [ObservableProperty]
    public partial double ExposureMax { get; set; } = 0;
    [ObservableProperty]
    public partial double ExposureStep { get; set; } = 0;

    public Camera.Camera[] Cameras => _client.GetCameras();
    public CameraStream[] Streams { get; private set; } = [];

    public CameraViewModel(CameraClient client)
    {
        _client = client;
        _client.AvailabilityChanged += (s, e) =>
        {
            _isCameraStreamReady = _client.IsCameraStreamReady;
            _isVideoFileNameSet = _client.VideoFileName.Length > 0;

            VideoFileName = _client.VideoFileName;
            IsRecording = _client.IsRecording;

            UpdateStreamList();

            Camera = _client.Camera;
            Stream = _client.Stream == null
                ? null
                : ToCameraStream(_client.Stream);

            if (Camera != null)
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    UpdateCameraControls(Camera.Id);
                });

            OnPropertyChanged(nameof(IsAvailable));

            _isInitilizing = false;
        };

        _client.RecordingChanged += (s, e) =>
        {
            IsRecording = e;
            Data = IsRecording ? "recording" : "stopped";
        };

        App.Current.Exit += (s, e) => _cameraProperties?.Dispose();
    }

    #region Internal

    readonly CameraClient _client;

    bool _isVideoFileNameSet = false;
    bool _isCameraStreamReady = false;
    bool _isInitilizing = true;
    CameraProperties? _cameraProperties;

    partial void OnIsRecordingChanged(bool value)
    {
        if (value)
        {
            if (_client.Start())
                Data = "recording...";
            else
                Data = "failed to start recording";
        }
        else
        {
            _client.Stop();
            Data = "stopped";
        }
    }

    partial void OnCameraChanged(Camera.Camera? value)
    {
        if (value != null && !_isInitilizing)
        {
            if (!_client.SetCamera(value))
            {
                Data = "failed to set the camera";
            }
            else
            {
                UpdateStreamList();
                UpdateCameraControls(value.Id);
            }
        }
    }

    partial void OnStreamChanged(CameraStream? value)
    {
        if (value != null && !_isInitilizing)
        {
            if (!_client.SetStream(value.Ref))
                Data = "failed to set the stream";
            else
            {
                _isCameraStreamReady = true;
                OnPropertyChanged(nameof(IsReadyToRecord)); 
            }
        }
    }

    partial void OnExposureChanged(double value)
    {
        _cameraProperties?.SetControlValue(CameraProperties.CameraControlProperty.Exposure, (int)value);
    }

    [RelayCommand]
    private void SetVideoFileName()
    {
        if (VideoFileName.IsValidPath())
        {
            if (!_client.SetVideoFileName(VideoFileName))
            {
                Data = "cannot set file name";
                VideoFileName = string.Empty;
            }
            else
            {
                Data = "file name was set";
                VideoFileName = _client.VideoFileName;
                _isVideoFileNameSet = true;
            }
        }
        else
        {
            VideoFileName = string.Empty;
            Data = "invalid file name";
        }

        OnPropertyChanged(nameof(IsReadyToRecord));
    }

    private void UpdateStreamList()
    {
        var streams = new List<CameraStream>();
        foreach (var stream in _client.GetStreams())
            streams.Add(ToCameraStream(stream));
        Streams = streams.ToArray();
        OnPropertyChanged(nameof(Streams));
    }

    void UpdateCameraControls(string cameraId)
    {
        _cameraProperties?.Dispose();

        try
        {
            _cameraProperties = new CameraProperties(cameraId);
            if (_cameraProperties.GetControlRangeExposure(
                CameraProperties.CameraControlProperty.Exposure,
                out var range))
            {
                ExposureMin = range!.Min;
                ExposureMax = range.Max;
                ExposureStep = range.Step;
                Exposure = range.DefaultValue;
            }
        }
        catch { }
    }
    private static CameraStream ToCameraStream(Camera.Stream stream) =>
        new(stream, $"{stream.Width} x {stream.Height}, {stream.Fps} Hz");

    #endregion
}