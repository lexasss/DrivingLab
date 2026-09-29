using CommunityToolkit.Mvvm.ComponentModel;
using ClientExample.Tools;
using CommunityToolkit.Mvvm.Input;

namespace ClientExample;

public partial class CameraViewModel : ObservableObject
{
    public record class CameraStream(Camera.Stream Ref, string Description);

    public bool IsAvailable => _client.IsAvailable;
    public bool IsReadyToRecord => _isCameraStreamReady && VideoFileName.IsValidPath();
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

    public Camera.Camera[] Cameras => _client.GetCameras();
    public CameraStream[] Streams { get; private set; } = [];

    public CameraViewModel(CameraClient client)
    {
        _client = client;
        _client.AvailabilityChanged += (s, e) =>
        {
            _isCameraStreamReady = _client.IsCameraStreamReady;

            IsRecording = _client.IsRecording;

            UpdateStreamList();

            Camera = _client.Camera;
            Stream = _client.Stream == null 
                ? null
                : ToCameraStream(_client.Stream);

            OnPropertyChanged(nameof(IsAvailable));

            _isInitilizing = false;
        };

        _client.RecordingChanged += (s, e) =>
        {
            IsRecording = e;
            Data = IsRecording ? "recording" : "stopped";
        };
    }

    #region Internal

    readonly CameraClient _client;

    bool _isCameraStreamReady = false;
    bool _isInitilizing = true;

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
                Data = "failed to set the camera";
            else
                UpdateStreamList();
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
                Data = "File name was set";
            }
        }
        else
        {
            Data = "Invalid file name";
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

    private static CameraStream ToCameraStream(Camera.Stream stream) =>
        new(stream, $"{stream.Width} x {stream.Height}, {stream.Fps} Hz");

    #endregion
}