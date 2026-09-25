using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using QSoft.MediaCapture;
using System.IO;
using Proto = global::Camera;

namespace Server.Camera;

internal class CameraService :
    Proto.Dispatcher.DispatcherBase,
    IService
{
    public bool IsAvailable() => _isAvailable;

    public CameraService(ILoggerFactory loggerFactory) : base()
    {
        _logger = loggerFactory.CreateLogger("CAMR");

        try
        {
            _cameras = WebCam_MF.GetAllWebCams().ToList();
            _isAvailable = true;

            if (!_cameras.Any())
            {
                _logger.LogWarning("No cameras found.");
            }
            else foreach (var camera in _cameras)
            {
                _logger.LogInformation("Found camera {name}", camera.FriendName);
                camera.MediaCaptureFailedEventHandler += Camera_MediaCaptureFailedEventHandler;
            }

            _baseService = new(_logger);

            _logger.LogInformation("Running");

            /*
            Task.Run(async () =>
            {
                await Task.Delay(5000);
                await SetLogFileName(new Common.String { Value = "recording.mp4" }, null);
                await Task.Delay(1000);
                var cams = await GetCameras(new Empty(), null);
                var cam = cams.Items.FirstOrDefault(c => c.Name.Contains("LifeCam"));
                if (cam != null)
                {
                    await Task.Delay(1000);
                    if ((await SetCamera(cam, null)).Value == true)
                    {
                        await Task.Delay(1000);
                        var streams = await GetStreams(new Empty(), null);
                        var stream = streams.Items.FirstOrDefault(s => s.Height == 720);
                        if (stream != null)
                        {
                            await Task.Delay(1000);
                            if ((await SetStream(stream, null)).Value == true)
                            {
                                await Task.Delay(1000);
                                if ((await Start(new Empty(), null)).Value == true)
                                {
                                    await Task.Delay(5000);
                                    await Stop(new Empty(), null);
                                }
                                else System.Diagnostics.Debug.WriteLine("cannot start");
                            }
                            else System.Diagnostics.Debug.WriteLine("cannot set stream");
                        }
                        else System.Diagnostics.Debug.WriteLine("no stream");
                    }
                    else System.Diagnostics.Debug.WriteLine("cannot set camera");
                }
                else System.Diagnostics.Debug.WriteLine("no camera");
            });*/
        }
        catch (Exception)
        {
            _logger.LogError("Cannot start the service");
        }
    }

    public void Dispose()
    {
        CloseCamera(true);

        _baseService?.Dispose();

        GC.SuppressFinalize(this);
    }

    public override Task<Common.Bool> IsAvailable(
        Empty request,
        ServerCallContext context)
    {
        return Common.Bool.From(IsAvailable());
    }

    public override Task<Common.Bool> IsRecording(
        Empty request,
        ServerCallContext context)
    {
        return Common.Bool.From(_isRecording);
    }

    public override Task<Common.Bool> SetLogFileName(
        Common.String request,
        ServerCallContext context)
    {
        if (_isRecording)
            return Common.Bool.False;

        var filename = request.Value;

        if (string.IsNullOrEmpty(filename))
            return Common.Bool.False;

        _videoFileName = filename;
        if (!_videoFileName.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
        {
            _videoFileName += ".mp4";
        }

        try
        {
            if (!Path.IsPathRooted(_videoFileName))
            {
                _videoFileName = Path.Combine(
                    AppContext.BaseDirectory,
                    DATA_FOLDER,
                    filename
                );
            }

            var folder = Path.GetDirectoryName(_videoFileName);
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder!);
            }
        }
        catch
        {
            _logger.LogError("Filename '{name}' cannot be set", filename);
            _videoFileName = string.Empty;
            return Common.Bool.False;
        }
        finally
        {
            _logger.LogError("Filename '{name}' was set", Path.GetFileName(_videoFileName));
        }

        return Common.Bool.True;
    }

    public override Task<Proto.Cameras> GetCameras(
        Empty request,
        ServerCallContext context) 
    {
        var result = new Proto.Cameras();
        foreach (var camera in _cameras)
            result.Items.Add(new Proto.Camera()
            {
                Name = camera.FriendName
            });
        return Task.FromResult(result);
    }

    public override Task<Common.Bool> SetCamera(
        Proto.Camera request,
        ServerCallContext context)
    {
        if (_isRecording)
            return Common.Bool.False;

        CloseCamera(false);

        _camera = _cameras.FirstOrDefault(cam => cam.FriendName == request.Name);
        if (_camera == null)
        {
            _logger.LogError("Camera '{name}' not found", request.Name);
            return Common.Bool.False;
        }

        var task = _camera.InitCaptureEngine(new WebCam_MF_Setting()
        {
            IsMirror = true,
            Rotate = CameraRotates.Rotate0,
            Shared = true,
            UseD3D = true,
        });
        task.Wait();

        if (task.Result != DirectN.HRESULTS.S_OK)
        {
            _logger.LogError("Camera '{name}' cannot be initilized", request.Name);
            return Common.Bool.False;
        }

        _streams = _camera.GetAvailableMediaStreamProperties(
            DirectN.MF_CAPTURE_ENGINE_STREAM_CATEGORY.MF_CAPTURE_ENGINE_STREAM_CATEGORY_VIDEO_CAPTURE)
            .Where(props => props.Width >= 640 && props.Fps >= 24)
            .ToArray();

        _logger.LogInformation("Camera '{name}' with {streams} available streams was initilized", request.Name, _streams.Length);

        return Common.Bool.True;
    }

    public override Task<Proto.Streams> GetStreams(
        Empty request,
        ServerCallContext context) 
    {
        var result = new Proto.Streams();
        foreach (var stream in _streams)
            result.Items.Add(new Proto.Stream()
            {
                Width = (int)stream.Width,
                Height = (int)stream.Height,
                Fps = stream.Fps
            });
        return Task.FromResult(result);
    }

    public override Task<Common.Bool> SetStream(
        Proto.Stream request, 
        ServerCallContext context) 
    {
        if (_isRecording || _camera == null)
            return Common.Bool.False;

        var stream = _streams.FirstOrDefault(stream => stream.StreamIndex == request.Index);
        if (stream == null)
            return Common.Bool.False;

        var task = _camera.SetMediaStreamPropertiesAsync(stream);
        task.Wait();

        if (string.IsNullOrEmpty(_videoFileName))
            _logger.LogInformation("Stream set to {w} x {h}, {fps} Hz",
                request.Width,
                request.Height,
                request.Fps);
        else
            _logger.LogInformation("Ready to record video as {w} x {h}, {fps} Hz to {filename}",
                request.Width,
                request.Height,
                request.Fps,
                Path.GetFileName(_videoFileName));

        _isReady = true;

        return Common.Bool.True;
    }

    public override Task<Common.Bool> Start(
        Empty request,
        ServerCallContext context)
    {
        if (_isRecording)
            return Common.Bool.False;

        if (_camera == null)
        {
            _logger.LogError("Cannot start video recording as {reason} was not specified yet", "camera");
            return Common.Bool.False;
        }

        if (!_isReady)
        {
            _logger.LogError("Cannot start video recording as {reason} was not specified yet", "stream");
            return Common.Bool.False;
        }

        if (string.IsNullOrEmpty(_videoFileName))
        {
            _logger.LogError("Cannot start video recording as {reason} was not specified yet", "filename");
            return Common.Bool.False;
        }

        try
        {
            var task = _camera.StartRecord(_videoFileName);
            task.Wait();
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to start video recording: {reason}", ex.Message);
            return Common.Bool.False;
        }
        finally
        {
            _isRecording = true;
            _logger.LogInformation("Video recording started");
        }

        return Common.Bool.True;
    }

    public override Task<Empty> Stop(
        Empty request,
        ServerCallContext context)
    {
        if (!_isRecording || _camera == null)
            return Common.Constants.Empty;

        try
        {
            var task = _camera.StopRecord();
            task.Wait();
        }
        finally
        {
            _isRecording = false;
            _logger.LogInformation("Video recording stopped");
        }

        return Common.Constants.Empty;
    }

    public override async Task ReadEvents(
        Empty request,
        IServerStreamWriter<Proto.Event> responseStream,
        ServerCallContext context)
    {
        if (_baseService == null)
            return;

        await _baseService.ReadEvents(request, responseStream, context);
    }

    #region Internal

    const string DATA_FOLDER = "video";

    readonly ILogger _logger;
    readonly Tools.Service<Proto.Event>? _baseService;
    readonly List<WebCam_MF> _cameras = [];

    WebCam_MF? _camera;
    ImageEncodingProperties[] _streams = [];

    bool _isAvailable = false;
    bool _isReady = false;
    bool _isRecording = false;
    string _videoFileName = string.Empty;

    private void CloseCamera(bool isDisposing)
    {
        if (_camera == null)
            return;

        if (!isDisposing)
            _logger.LogInformation("Closing camera '{name}'", _camera.FriendName);

        try
        {
            if (_isRecording)
            {
                var task = _camera.StopRecord();
                task.Wait();
            }
        }
        catch { }

        _streams = [];
        _isReady = false;
        _isRecording = false;
        _camera = null;
    }

    private void Camera_MediaCaptureFailedEventHandler(object? sender, MediaCaptureFailedEventArgs e)
    {
        var self = (WebCam_MF?)sender;
        if (self == _camera)
        {
            if (_isRecording)
            {
                _isRecording = false;
                _baseService?.Publish(new Proto.Event()
                {
                    IsRecording = _isRecording,
                });
            }

            CloseCamera(false);
        }
    }

    #endregion
}
