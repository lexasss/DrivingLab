using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using System.IO;
using Proto = global::Screen;

namespace Server.Screen;

public class ScreenService : Proto.Dispatcher.DispatcherBase, IFileService
{
    public bool IsAvailable() => true;

    public ScreenService(ILoggerFactory loggerFactory) : base()
    {
        _logger = loggerFactory.CreateLogger("SCRN");

        UpdateScreenList();

        foreach (var screen in _screens)
        {
            _logger.LogInformation("Found screen {name} at ({x},{y}) with size {width}x{height}",
                screen.Name, screen.Origin.X, screen.Origin.Y, screen.Size.Width, screen.Size.Height);
        }

        Tools.Helpers.ListFiles(
            MEDIA_FOLDER,
            _supportedMediaFormats,
            _logger
        );

        _logger.LogInformation("Running");
    }

    public void Dispose()
    {
        _isActive = false;

        foreach (var media in _media.Values)
        {
            media.Close();
        }

        _logger.LogInformation("Disposed");

        GC.SuppressFinalize(this);
    }

    public override Task<Common.Bool> IsAvailable(Empty request, ServerCallContext context)
    {
        return Task.FromResult(new Common.Bool { Value = true });
    }

    public override async Task<Proto.Screens> GetScreens(Empty request, ServerCallContext context)
    {
        var result = new Proto.Screens();

        if (_screens.Count == 0)
        {
            UpdateScreenList();
        }

        foreach (var screen in _screens)
        {
            result.Items.Add(screen);
        }

        return result;
    }

    public override async Task ReadEvents(
        Empty request,
        IServerStreamWriter<Proto.Event> responseStream,
        ServerCallContext context)
    {
        while (_isActive && !context.CancellationToken.IsCancellationRequested)
        {
            await Task.Delay(5);

            if (_events.Count > 0)
            {
                var evt = _events.Dequeue();
                await responseStream.WriteAsync(evt);
            }
        }
    }

    public override Task<Common.String> Show(Proto.Media request, ServerCallContext context)
    {
        string id = string.Empty;

        var filePath = request.FileName;
        if (!Path.IsPathRooted(filePath))
        {
            filePath = Path.Combine(AppContext.BaseDirectory, MEDIA_FOLDER, filePath);
        }

        if (File.Exists(filePath))
        {
            try
            {
                var screen = _screens.FirstOrDefault(s => s.Id == request.ScreenId) ?? _screens.First();

                var mediaWindow = new MediaWindow();
                id = mediaWindow.Id;
                mediaWindow.Show(filePath,
                    new Common.Point {
                        X = screen.Origin.X + request.Location.X,
                        Y = screen.Origin.Y + request.Location.Y
                    },
                    request.Size,
                    request.Duration);

                mediaWindow.Shown += (sender, success) =>
                {
                    if (success)
                    {
                        _logger.LogInformation("Showing {filename}",
                            Path.GetFileNameWithoutExtension(request.FileName));
                        _media[id] = mediaWindow;
                    }
                    else
                    {
                        _logger.LogError("Media type of {filename} is not supported",
                            Path.GetFileName(request.FileName));
                    }
                };
                mediaWindow.Hidden += (sender, mediaId) =>
                {
                    if (_media.TryGetValue(mediaId, out MediaWindow? value))
                    {
                        _logger.LogInformation("Image {name} was hidden", value.Name);
                        _media.Remove(mediaId);
                        _events.Enqueue(new Proto.Event
                        {
                            Name = Proto.Events.MEDIA_HIDDEN,
                            Value = mediaId
                        });
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing {filename}: {reason}",
                    request.FileName, ex.Message);
            }
        }
        else
        {
            _logger.LogWarning("File not found: {filename}", filePath);
        }

        return Task.FromResult(new Common.String { Value = id });
    }

    public override Task<Empty> Close(Common.String request, ServerCallContext context)
    {
        if (_media.TryGetValue(request.Value, out var mediaWindow))
        {
            mediaWindow.Close();
            _media.Remove(request.Value);
            _logger.LogInformation("Closing the media {name}", mediaWindow.Name);
        }

        return Task.FromResult(new Empty());
    }

    public override async Task<Common.UploadResult> UploadFile(
        IAsyncStreamReader<Common.UploadRequest> requestStream,
        ServerCallContext context)
    {
        var filename = requestStream.Current?.Metadata?.FileName;
        var result = await Tools.Helpers.UploadFile(requestStream, context, MEDIA_FOLDER);

        if (result.Size > 0)
            _logger.LogInformation("Uploaded {name} ({size} bytes)",
                filename, result.Size);
        else
            _logger.LogWarning("Upload failed for {name}: {error}",
                filename, result.ErrorMessage);

        return result;
    }
    
    #region Internal

    const string MEDIA_FOLDER = "media";

    readonly string[] _supportedMediaFormats = [
        .. MediaWindow.SupportedImageFormats,
        .. MediaWindow.SupportedVideoFormats];

    readonly ILogger _logger;
    readonly Queue<Proto.Event> _events = [];
    readonly List<Proto.Screen> _screens = [];
    readonly Dictionary<string, MediaWindow> _media = [];

    bool _isActive = true;

    private void UpdateScreenList()
    {
        _screens.Clear();
        foreach (var screen in ScreenEnumerator.EnumerateScreens())
            _screens.Add(new Proto.Screen
            {
                Id = screen.Id,
                Name = screen.Name,
                Origin = new Common.Point { X = screen.X, Y = screen.Y },
                Size = new Common.Size { Width = screen.Width, Height = screen.Height }
            });
    }

    #endregion
}
