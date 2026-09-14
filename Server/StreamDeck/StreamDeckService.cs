using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using OpenMacroBoard.SDK;
using SixLabors.ImageSharp;
using System.IO;
using Proto = global::StreamDeck;

namespace Server.StreamDeck;

internal class StreamDeckService : Proto.Dispatcher.DispatcherBase, IFileService
{
    public bool IsAvailable() => StreamDeckSharp.StreamDeck.EnumerateDevices().Any();

    public StreamDeckService(ILoggerFactory loggerFactory) : base()
    {
        _logger = loggerFactory.CreateLogger("DECK");
        _isActive = true;

        if (IsAvailable())
            Connect();
        else
            _logger.LogWarning("Found no Stream Deck devices");
    }

    public void Dispose()
    {
        _isActive = false;
        _isConnected = false;

        _deck?.Dispose();

        _logger.LogInformation("Disposed");

        GC.SuppressFinalize(this);
    }

    public override Task<Common.Bool> IsAvailable (Empty request, ServerCallContext context)
    {
        return Task.FromResult(new Common.Bool { Value = IsAvailable() });
    }

    public override async Task<Common.UploadResult> UploadFile(
        IAsyncStreamReader<Common.UploadRequest> requestStream,
        ServerCallContext context)
    {
        var filename = requestStream.Current?.Metadata?.FileName;
        var result = await Tools.Helpers.UploadFile(requestStream, context, ICONS_FOLDER);

        if (result.Size > 0)
            _logger.LogInformation("Uploaded {name} ({size} bytes)",
                filename, result.Size);
        else
            _logger.LogWarning("Upload failed for {name}: {error}",
                filename, result.ErrorMessage);

        return result;
    }

    public override Task<Proto.Keyboard> GetKeyboard(Empty request, ServerCallContext context)
    {
        return Task.FromResult(new Proto.Keyboard {
            Count = _deck?.Keys.Count ?? 0,
            Rows = _deck?.Keys.CountY ?? 0,
            Columns = _deck?.Keys.CountX ?? 0,
            Size = _deck?.Keys.KeySize ?? 0,
            Gap = _deck?.Keys.GapSize ?? 0,
        });
    }

    public override Task<Empty> SetBrightness(Common.Int request, ServerCallContext context)
    {
        _deck?.SetBrightness((byte)Math.Clamp(request.Value, 0, 100));
        return Task.FromResult(new Empty());
    }

    public override Task<Common.Bool> SetKey(Proto.Key request, ServerCallContext context)
    {
        bool result = false;
        if (request.Id < 0)
        {
            if (string.IsNullOrEmpty(request.FileNameOrColor))
            {
                _deck.ClearKeys();
                result = true;
            }
            else
            {
                var filePath = request.FileNameOrColor;
                if (!Path.IsPathRooted(filePath))
                {
                    filePath = Path.Combine(AppContext.BaseDirectory, ICONS_FOLDER, filePath);
                }

                if (File.Exists(filePath))
                {
                    var bmp = Image.Load(filePath);
                    _deck.DrawFullScreenBitmap(bmp);
                    result = true;
                }
                else
                {
                    _logger.LogWarning("File not found: {filename}", filePath);
                }
            }
        }
        else if (request.Id < _deck?.Keys.Count)
        {
            var color = FromRGB(request.FileNameOrColor);
            if (color != OmbColor.Black)
            {
                var bmp = KeyBitmap.Create.FromColor(color);
                _deck.SetKeyBitmap(request.Id, bmp);
                result = true;
            }
            else
            {
                var filePath = request.FileNameOrColor;
                if (!Path.IsPathRooted(filePath))
                {
                    filePath = Path.Combine(AppContext.BaseDirectory, ICONS_FOLDER, filePath);
                }

                if (File.Exists(filePath))
                {
                    var bmp = KeyBitmap.Create.FromFile(filePath);
                    _deck.SetKeyBitmap(request.Id, bmp);
                    result = true;
                }
                else
                {
                    _logger.LogWarning("File not found: {filename}", filePath);
                }
            }
        }

        return Task.FromResult(new Common.Bool() { Value = result });
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

    #region Internal

    const string ICONS_FOLDER = "icons";

    readonly ILogger _logger;
    readonly Queue<Proto.Event> _events = [];

    IMacroBoard? _deck;

    bool _isActive = false;
    bool _isConnected = false;

    private void Connect()
    {
        try
        {
            _deck = StreamDeckSharp.StreamDeck.OpenDevice();
            _deck.ConnectionStateChanged += (s, e) =>
            {
                Task.Run(() =>
                {
                    if (e.NewConnectionState && _deck == null)
                    {
                        Connect();
                    }
                    else if (!e.NewConnectionState && _deck != null)
                    {
                        _isConnected = false;
                        _events.Enqueue(new Proto.Event() { IsConnected = _isConnected });

                        _deck.Dispose();
                        _deck = null;
                    }
                });
            };
            _deck.KeyStateChanged += (s, e) =>
            {
                _events.Enqueue(new Proto.Event()
                {
                    Key = new Proto.KeyState()
                    {
                        Id = e.Key,
                        IsPressed = e.IsDown
                    }
                });
            };

            _isConnected = true;

            _events.Enqueue(new Proto.Event() { IsConnected = _isConnected });

            _logger.LogInformation("Found stream deck {sn}: {row}x{col}", _deck.GetSerialNumber(), _deck.Keys.CountY, _deck.Keys.CountX);
            _logger.LogInformation("Running");
        }
        catch (Exception ex)
        {
            _logger.LogError("Cannot start the service ({ex})", ex.Message);
        }
    }

    private static OmbColor FromRGB(string rgb)
    {
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
