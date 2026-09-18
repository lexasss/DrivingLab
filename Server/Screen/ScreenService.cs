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

        Tools.FileHelper.ListFiles(
            StorageFolder,
            _supportedMediaFormats,
            _logger
        );

        _baseService = new(_logger);
    }

    public void Dispose()
    {
        foreach (var media in _media.Values)
        {
            media.Close();
        }

        _baseService.Dispose();

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
        await _baseService.ReadEvents(request, responseStream, context);
    }

    public override Task<Common.String> Show(
        Proto.Media request,
        ServerCallContext context)
    {
        string? filePath = Tools.FileHelper.FileNameToPath(
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
        return await Tools.FileHelper.UploadFile(
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

    readonly Dictionary<(string, int?), string> _knownScreens = new()
    {
        { ("NV Surround", null), "Main" },
        { ("WIMAXIT", 0), "Left mirror" },
        { ("WIMAXIT", null), "Right mirror" },
        { ("12.3FHD", null), "Rear view" },
        { ("L29w-30", null), "Dashboard" },
    };

    readonly ILogger _logger;
    readonly Tools.Service<Proto.Event> _baseService;
    readonly List<Proto.Screen> _screens = [];
    readonly Dictionary<string, MediaWindow> _media = [];
    readonly WindowPool _pool = new();

    private void UpdateScreenList()
    {
        _screens.Clear();
        foreach (var screen in ScreenEnumerator.EnumerateScreens())
        {
            _screens.Add(new Proto.Screen
            {
                Id = screen.Id,
                Name = ToKnownScreenName(screen),
                Origin = new Common.Point
                {
                    X = screen.X,
                    Y = screen.Y
                },
                Size = new Common.Size
                {
                    Width = screen.Width,
                    Height = screen.Height
                }
            });
        }
    }

    private string ToKnownScreenName(Screen screen)
    {
        var sameNameScreens = _knownScreens.Where(kv => kv.Key.Item1 == screen.Name);

        if (sameNameScreens.Count() == 1)
        {
            return sameNameScreens.First().Value;
        }
        else if (sameNameScreens.Count() > 1)
        {
            foreach (var kv in sameNameScreens)
            {
                if (kv.Key.Item2 == screen.X || kv.Key.Item2 == null)
                {
                    return kv.Value;
                }
            }
        }

        return screen.Name;
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
            _baseService.Publish(new Proto.Event {
                HiddenMediaId = mediaId
            });
        }
    }

    #endregion
}
