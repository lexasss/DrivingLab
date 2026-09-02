using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using System.IO;
using Proto = global::Screen;

namespace Server.Screen;

public class ScreenService : Proto.Dispatcher.DispatcherBase, IService
{
    public bool IsAvailable() => true;

    public ScreenService(ILogger<ScreenService> logger) : base()
    {
        _logger = logger;

        UpdateScreenList();

        foreach (var screen in _screens)
        {
            _logger.LogInformation("[SCRN] Found screen {id} ({name}) at ({x},{y}) with size {width}x{height}",
                screen.Id, screen.Name, screen.Origin.X, screen.Origin.Y, screen.Size.Width, screen.Size.Height);
        }

        try
        {
            foreach (var file in Directory.EnumerateFiles(MEDIA_FOLDER, "*.png"))
            {
                _logger.LogInformation("[SCRN] Found image file {file}", Path.GetFileNameWithoutExtension(file));
            }
            foreach (var file in Directory.EnumerateFiles(MEDIA_FOLDER, "*.mp4"))
            {
                _logger.LogInformation("[SCRN] Found video file {file}", Path.GetFileNameWithoutExtension(file));
            }
        }
        catch
        {
            _logger.LogWarning("[SCRN] Media folder does not exist");
        }

        _logger.LogInformation("[SCRN] Running");
    }

    public void Dispose()
    {
        _isActive = false;

        _logger.LogInformation("[SCRN] Disposed");

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

    public override async Task ReadEvents(Empty request, IServerStreamWriter<Proto.Event> responseStream, ServerCallContext context)
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

    public override Task<Common.String> Show(Proto.Image request, ServerCallContext context)
    {
        string? id = null;

        var filePath = request.FileName;
        if (!Path.IsPathRooted(filePath))
        {
            filePath = Path.Combine(AppContext.BaseDirectory, MEDIA_FOLDER, filePath);
        }

        if (File.Exists(filePath))
        {
            try
            {
                var screen = _screens.First(s => s.Id == request.ScreenId);

                var mediaWindow = new MediaWindow();
                mediaWindow.Show(filePath,
                    new Common.Point {
                        X = screen.Origin.X + request.Location.X,
                        Y = screen.Origin.Y + request.Location.Y
                    },
                    request.Size,
                    request.Duration);


                id = mediaWindow.Id;
                _media[id] = mediaWindow;

                mediaWindow.Hidden += (sender, mediaId) =>
                {
                    if (_media.ContainsKey(mediaId))
                    {
                        _logger.LogInformation("[SNDP] Image {name} was hidden", _media[mediaId].Name);
                        _media.Remove(mediaId);
                    }
                };

                _logger.LogInformation("[SNDP] Showing {filename}", Path.GetFileNameWithoutExtension(request.FileName));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SNDP] Error showing {filename}: {reason}", request.FileName, ex.Message);
            }
        }
        else
        {
            _logger.LogWarning("[SNDP] File not found: {filename}", filePath);
        }

        return Task.FromResult(new Common.String { Value = id });
    }

    public override Task<Empty> Close(Common.String request, ServerCallContext context)
    {
        if (_media.TryGetValue(request.Value, out var mediaWindow))
        {
            mediaWindow.Close();
            _media.Remove(request.Value);
            _logger.LogInformation("[SNDP] Closing the media {name}", mediaWindow.Name);
        }

        return Task.FromResult(new Empty());
    }

    #region Internal

    const string MEDIA_FOLDER = "media";

    readonly ILogger<ScreenService> _logger;
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
                Name = screen.DeviceName,
                Origin = new Common.Point { X = screen.X, Y = screen.Y },
                Size = new Common.Size { Width = screen.Width, Height = screen.Height }
            });
    }

    #endregion
}
