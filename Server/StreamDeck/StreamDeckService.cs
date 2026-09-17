using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using OpenMacroBoard.SDK;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Proto = global::StreamDeck;

namespace Server.StreamDeck;

internal class StreamDeckService : Proto.Dispatcher.DispatcherBase, IFileService
{
    public string StorageFolder { get; } = "deck";

    public bool IsAvailable() => StreamDeckSharp.StreamDeck
        .EnumerateDevices()
        .Any();

    public StreamDeckService(ILoggerFactory loggerFactory) : base()
    {
        _logger = loggerFactory.CreateLogger("DECK");

        if (IsAvailable())
        {
            if (Connect())
            {
                _baseService = new(_logger);
            }
        }
        else
        {
            _logger.LogWarning("Found no Stream Deck devices");
        }
    }

    public void Dispose()
    {
        _isConnected = false;

        _deck?.Dispose();
        _baseService?.Dispose();

        GC.SuppressFinalize(this);
    }

    public override Task<Common.Bool> IsAvailable(
        Empty request,
        ServerCallContext context)
    {
        return Common.Bool.From(IsAvailable());
    }

    public override Task<Common.Bool> IsConnected(
        Empty request,
        ServerCallContext context)
    {
        return Common.Bool.From(_isConnected);
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

    public override Task<Proto.Keyboard> GetKeyboard(
        Empty request,
        ServerCallContext context)
    {
        if (!_isConnected)
            return Task.FromResult(new Proto.Keyboard());

        return Task.FromResult(new Proto.Keyboard {
            Count = _deck?.Keys.Count ?? 0,
            Rows = _deck?.Keys.CountY ?? 0,
            Columns = _deck?.Keys.CountX ?? 0,
            Size = _deck?.Keys.KeySize ?? 0,
            Gap = _deck?.Keys.GapSize ?? 0,
        });
    }

    public override Task<Empty> SetBrightness(
        Common.Int request,
        ServerCallContext context)
    {
        if (_isConnected)
        {
            var brightness = (byte)Math.Clamp(request.Value, 0, 100);
            _deck?.SetBrightness(brightness);
            _logger.LogInformation("Brightness set to {brightness}", brightness);
        }
        return Common.Constants.Empty;
    }

    public override Task<Common.Bool> SetKey(
        Proto.Key request,
        ServerCallContext context)
    {
        if (_deck == null || !_isConnected)
            return Common.Bool.False;

        bool result = false;

        string? filePath = Tools.FileHelper.FileNameToPath(
            request.FileNameOrColor,
            StorageFolder,
            null);
        var color = FromRGB(request.FileNameOrColor);

        if (request.Id < 0)
        {
            if (color != OmbColor.Black)
            {
                var image = new Image<Rgba32>(
                    _deck.Keys.Area.Width,
                    _deck.Keys.Area.Height,
                    Color.FromRgb(color.R, color.G, color.B)
                         .ToPixel<Rgba32>()
                );
                _deck.DrawFullScreenBitmap(image);
                _logger.LogInformation("All keys set to {color}", color);
                result = true;
            }
            else if (!string.IsNullOrEmpty(filePath))
            {
                var image = Image.Load(filePath);
                _deck.DrawFullScreenBitmap(image);
                _logger.LogInformation("The deck got {name} image",
                    System.IO.Path.GetFileName(filePath));
                result = true;
            }
            else if (string.IsNullOrEmpty(request.FileNameOrColor))
            {
                _deck.ClearKeys();
                _logger.LogInformation("All keys were cleared");
                result = true;
            }
        }
        else if (request.Id < _deck?.Keys.Count)
        {
            if (color != OmbColor.Black)
            {
                var bmp = KeyBitmap.Create.FromColor(color);
                _deck.SetKeyBitmap(request.Id, bmp);
                _logger.LogInformation("Key {id} set to {color}", request.Id, color);
                result = true;
            }
            else if (!string.IsNullOrEmpty(filePath))
            {
                var bmp = KeyBitmap.Create.FromFile(filePath);
                _deck.SetKeyBitmap(request.Id, bmp);
                _logger.LogInformation("Key {id} got {name} image",
                    request.Id,
                    System.IO.Path.GetFileName(filePath));
                result = true;
            }
            else if (string.IsNullOrEmpty(request.FileNameOrColor))
            {
                _deck.ClearKey(request.Id);
                _logger.LogInformation("Key {id} cleared", request.Id);
                result = true;
            }
        }

        if (!result)
        {
            _logger.LogError("Failed to execute the request with ID={id} and parameter '{param}'.",
                request.Id,
                request.FileNameOrColor);
        }

        return Common.Bool.From(result);
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

    readonly ILogger _logger;
    readonly Tools.Service<Proto.Event>? _baseService;

    IMacroBoard? _deck;

    bool _isConnected = false;

    private bool Connect()
    {
        try
        {
            _deck = StreamDeckSharp.StreamDeck.OpenDevice();
            _deck.ConnectionStateChanged += (s, e) =>
            {
                _isConnected = e.NewConnectionState;
                if (_baseService?.IsActive == true)
                {
                    _logger.LogInformation(_isConnected
                        ? "Stream Deck connected"
                        : "Stream Deck disconnected");
                    _baseService.Publish(new Proto.Event() {
                        IsConnected = _isConnected
                    });
                }
            };
            _deck.KeyStateChanged += (s, e) =>
            {
                _baseService?.Publish(new Proto.Event()
                {
                    Key = new Proto.KeyState()
                    {
                        Id = e.Key,
                        IsPressed = e.IsDown
                    }
                });
            };

            _isConnected = true;

            _logger.LogInformation("Found Stream Deck {sn}: {row}x{col}",
                _deck.GetSerialNumber(),
                _deck.Keys.CountY,
                _deck.Keys.CountX);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError("Cannot start the service ({ex})", ex.Message);
        }

        return false;
    }

    private static OmbColor FromRGB(string rgb)
    {
        // black color means "not a valid color"

        if (rgb == null)
            return OmbColor.Black;

        var p = rgb.Split(',');
        if (p.Length != 3)
            return OmbColor.Black;

        try
        {
            byte[] values = p.Select(byte.Parse).ToArray();
            return OmbColor.FromRgb(values[0], values[1], values[2]);
        }
        catch
        {
            return OmbColor.Black;
        }
    }

    #endregion
}
