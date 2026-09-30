using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Options;
using Dashboard.Tools;

namespace Dashboard;

public class CameraClient : Client
{
    public event EventHandler<bool>? RecordingChanged;

    public bool IsCameraStreamReady => _isCameraStreamReady;
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
            filename = $"{DateTime.Now:u}".ToPath();
        }

        return _client.SetVideoFileName(new Common.String() { Value = filename }).Value;
    }

    #region Internal

    readonly Camera.Dispatcher.DispatcherClient _client;

    bool _isCameraStreamReady = false;
    bool _isRecording = false;
    Camera.Camera? _camera = null;
    Camera.Stream? _stream = null;

    AsyncServerStreamingCall<Camera.Event>? _eventsCall;

    protected override void Initialize()
    {
        _isAvailable = _client.IsAvailable(new Empty()).Value;
        if (_isAvailable)
        {
            _isCameraStreamReady = _client.IsCameraStreamReady(new Empty()).Value;
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

    private async Task ReadEvents()
    {
        try
        {
            _eventsCall = _client.ReadEvents(new Empty());
            var responseStream = _eventsCall.ResponseStream;

            while (await responseStream.MoveNext(_eventsCts.Token))
            {
                if (_eventsCts.IsCancellationRequested)
                    break;

                var evt = responseStream.Current;
                switch (evt.ValueCase)
                {
                    case global::Camera.Event.ValueOneofCase.IsRecording:
                        RecordingChanged?.Invoke(this, evt.IsRecording);
                        break;
                    default:
                        System.Diagnostics.Debug.WriteLine($"Camera event '{evt.ValueCase}' is not supported");
                        break;
                }
            }
        }
        catch (RpcException ex)
        {
            LogException(ex);
        }
        finally
        {
            _eventsCall = null;
        }
    }

    #endregion
}