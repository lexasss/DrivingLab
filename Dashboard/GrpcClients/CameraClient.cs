using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Options;
using Dashboard.Tools;

namespace Dashboard.GrpcClients;

public sealed class CameraClient 
    : Client<Camera.Dispatcher.DispatcherClient, Camera.Event>
{
    public event EventHandler<bool>? RecordingChanged;

    public bool IsCameraStreamReady => _isCameraStreamReady;
    public string VideoFileName => _videoFileName;
    public bool IsRecording => _isRecording;
    public Camera.Camera? Camera => _camera;
    public Camera.Stream? Stream => _stream;

    public CameraClient(IOptions<AppSettings> appSettings)
        : base(appSettings, (int)Common.Ports.Camera)
    {
        _client = new Camera.Dispatcher.DispatcherClient(_channel);
    }

    public override void Dispose()
    {
        _eventsCall?.Dispose();

        base.Dispose();
    }

    public Camera.Camera[] GetCameras()
    {
        if (!_isAvailable)
            return [];

        var cameras = _client.GetCameras(new Empty());
        return cameras.Items.ToArray();
    }

    public bool SetCamera(Camera.Camera camera) 
    {
        if (!_isAvailable)
            return false;

        var result = _client.SetCamera(camera).Value;
        if (result)
            _camera = camera;

        return result;
    }

    public Camera.Stream[] GetStreams()
    {
        if (!_isAvailable)
            return [];

        var streams = _client.GetStreams(new Empty());
        return streams.Items.ToArray();
    }

    public bool SetStream(Camera.Stream stream) 
    {
        if (!_isAvailable)
            return false;

        var result = _client.SetStream(stream).Value;
        _isCameraStreamReady = result || _isCameraStreamReady;

        if (result)
            _stream = stream;

        return result;
    }

    public bool Start()
    {
        if (!_isAvailable)
            return false;

        _isRecording = _client.Start(new Empty()).Value;
        return _isRecording;
    }

    public void Stop()
    {
        if (!_isAvailable)
            return;

        _isRecording = false;
        _ = _client.Stop(new Empty());
    }

    public bool SetVideoFileName(string? filename = null)
    {
        if (!_isAvailable)
            return false;

        if (string.IsNullOrEmpty(filename))
        {
            filename = $"{DateTime.Now:u}.mp4".ToPath();
        }

        var isSuccess = _client.SetVideoFileName(new Common.String() {
            Value = filename
        }).Value;

        if (isSuccess)
        {
            _videoFileName = filename;
        }

        return isSuccess;
    }

    #region Internal

    bool _isCameraStreamReady = false;
    string _videoFileName = string.Empty;
    bool _isRecording = false;
    Camera.Camera? _camera = null;
    Camera.Stream? _stream = null;

    protected override void Initialize()
    {
        _isAvailable = _client.IsAvailable(new Empty()).Value;
        if (_isAvailable)
        {
            _isCameraStreamReady = _client.IsCameraStreamReady(new Empty()).Value;
            _videoFileName = _client.GetVideoFileName(new Empty()).Value;
            _isRecording = _client.IsRecording(new Empty()).Value;

            try
            {
                _camera = _client.GetCurrentCamera(new Empty());
                _stream = _client.GetCurrentStream(new Empty());
            }
            catch { }

            _ = ReadEvents();
        }
    }

    protected override IAsyncStreamReader<Camera.Event> GetEventStream()
    {
        _eventsCall = _client.ReadEvents(new Empty());
        return _eventsCall.ResponseStream;
    }

    protected override bool HandleEvent(Camera.Event evt)
    {
        switch (evt.ValueCase)
        {
            case global::Camera.Event.ValueOneofCase.IsRecording:
                RecordingChanged?.Invoke(this, evt.IsRecording);
                break;
            default:
                return false;
        }

        return true;
    }

    #endregion
}