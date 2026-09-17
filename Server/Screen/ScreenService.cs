using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using System.IO;
using Proto = global::Screen;

namespace Server.Screen;

public class ScreenService :
    Proto.Dispatcher.DispatcherBase,
    IFileService
{
    public string StorageFolder { get; } = "media";
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

        Tools.FileService.ListFiles(
            StorageFolder,
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

    public override Task<Common.Bool> IsAvailable(
        Empty request,
        ServerCallContext context)
    {
        return Common.Bool.True;
    }

    public override async Task<Proto.Screens> GetScreens(
        Empty request,
        ServerCallContext context)
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

    public override Task<Common.String> Show(
        Proto.Media request,
        ServerCallContext context)
    {
        string? filePath = Tools.FileService.FileNameToPath(
            request.FileName,
            StorageFolder,
            _logger);

        if (string.IsNullOrEmpty(filePath))
            return Common.String.Empty;

        string id = string.Empty;

        try
        {
            var screen = _screens.FirstOrDefault(s => s.Id == request.ScreenId)
                ?? _screens.First();

            var mediaWindow = _pool.Obtain();
            mediaWindow.Shown += MediaWindow_Shown;
            mediaWindow.Hidden += MediaWindow_Hidden;

            id = mediaWindow.Id;

            mediaWindow.Show(
                filePath,
                new Common.Point {
                    X = screen.Origin.X + request.Location.X,
                    Y = screen.Origin.Y + request.Location.Y
                },
                request.Size,
                request.Duration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error showing {filename}: {reason}",
                request.FileName, ex.Message);
        }

        return Common.String.From(id);
    }

    public override Task<Empty> Close(Common.String request, ServerCallContext context)
    {
        if (_media.TryGetValue(request.Value, out var mediaWindow))
        {
            mediaWindow.Close();
            _media.Remove(request.Value);
            _logger.LogInformation("Closing the media {name}", mediaWindow.FileName);
        }

        return Common.Constants.Empty;
    }

    public override async Task<Common.UploadResult> UploadFile(
        IAsyncStreamReader<Common.UploadRequest> requestStream,
        ServerCallContext context)
    {
        return await Tools.FileService.UploadFile(
            requestStream,
            context,
            StorageFolder,
            _logger);
    }

    #region Internal

    readonly string[] _supportedMediaFormats = [
        ..MediaWindow.SupportedImageFormats,
        ..MediaWindow.SupportedVideoFormats
    ];

    readonly ILogger _logger;
    readonly Queue<Proto.Event> _events = [];
    readonly List<Proto.Screen> _screens = [];
    readonly Dictionary<string, MediaWindow> _media = [];
    readonly WindowPool _pool = new();

    bool _isActive = true;

    private void UpdateScreenList()
    {
        _screens.Clear();
        foreach (var screen in ScreenEnumerator.EnumerateScreens())
            _screens.Add(new Proto.Screen
            {
                Id = screen.Id,
                Name = screen.Name,
                Origin = new Common.Point {
                    X = screen.X,
                    Y = screen.Y
                },
                Size = new Common.Size {
                    Width = screen.Width,
                    Height = screen.Height
                }
            });
    }

    private void MediaWindow_Shown(object? sender, bool success)
    {
        var mediaWindow = (MediaWindow)sender!;
        if (success)
        {
            _logger.LogInformation("Showing {filename}",
                Path.GetFileNameWithoutExtension(mediaWindow.FileName));
            _media[mediaWindow.Id] = mediaWindow;
        }
        else
        {
            _logger.LogError("Media type of {filename} is not supported",
                Path.GetFileName(mediaWindow.FileName));
        }
    }

    private void MediaWindow_Hidden(object? sender, string mediaId)
    {
        if (_media.TryGetValue(mediaId, out MediaWindow? value))
        {
            _logger.LogInformation("Image {name} was hidden", value.FileName);
            _media.Remove(mediaId);
            _events.Enqueue(new Proto.Event {
                HiddenMediaId = mediaId
            });
        }
    }

    #endregion
}
