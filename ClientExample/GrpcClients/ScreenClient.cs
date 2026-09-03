using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Options;

namespace ClientExample;

public class ScreenClient : IDisposable
{
    public bool IsAvailable => _isAvailable;

    public event EventHandler<bool>? AvailabilityChanged;
    public event EventHandler<string>? MediaHidden;

    public ScreenClient(IOptions<AppSettings> appSettings)
    {
        _channel = new Channel(appSettings.Value.ServerIp, (int)Common.Ports.Screen, ChannelCredentials.Insecure);
        _client = new Screen.Dispatcher.DispatcherClient(_channel);

        Task.Run(Initialize);
    }

    public void Dispose()
    {
        _eventsCts.Cancel();
        _eventsCall?.Dispose();

        _channel.ShutdownAsync().Wait();

        GC.SuppressFinalize(this);
    }

    public Screen.Screens GetScreens()
    {
        return _isAvailable ? _client.GetScreens(new Empty()) : new Screen.Screens();
    }

    public async Task<string?> Show(
        string filename,
        int screenId,
        Common.Point location,
        Common.Size? size,
        int? duration)
    {
        if (!_isAvailable)
            return null;

        var response = await _client.ShowAsync(new Screen.Media()
        {
            FileName = filename,
            ScreenId = screenId,
            Location = location,
            Size = size,
            Duration = duration ?? 0
        });
        return string.IsNullOrEmpty(response?.Value) ? null : response.Value;
    }

    public void Hide(string id)
    {
        if (!_isAvailable)
            return;

        _ = _client.Close(new Common.String { Value = id });
    }

    #region Internal

    readonly Channel _channel;
    readonly Screen.Dispatcher.DispatcherClient _client;
    readonly CancellationTokenSource _eventsCts = new();
    
    bool _isAvailable = false;

    AsyncServerStreamingCall<Screen.Event>? _eventsCall;

    private async Task Initialize()
    {
        try
        {
            _isAvailable = _client.IsAvailable(new Empty()).Value;
            if (_isAvailable)
            {
                _ = ReadEvents();
            }
        }
        catch (RpcException ex)
        {
            LogException(ex);
        }
        finally
        {
            AvailabilityChanged?.Invoke(this, _isAvailable);
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
                if (evt.Name == Screen.Events.MEDIA_HIDDEN)
                {
                    MediaHidden?.Invoke(this, evt.Value);
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

    private static void LogException(Exception ex)
    {
        System.Diagnostics.Debug.WriteLine(ex.Message);
    }

    #endregion
}