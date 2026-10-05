using Grpc.Core;
using Microsoft.Extensions.Options;

namespace Dashboard.GrpcClients;

public abstract class Client<T, U> : IDisposable
    where T : ClientBase<T>
    where U : Google.Protobuf.IMessage<U>
{
    public bool IsAvailable => _isAvailable;

    public event EventHandler<bool>? AvailabilityChanged;

    public Client(IOptions<AppSettings> appSettings, int port)
    {
        _channel = new Channel(
            appSettings.Value.ServerIp,
            port,
            ChannelCredentials.Insecure);

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(10); // to be started after the instance is created
                Initialize();
            }
            catch (RpcException ex)
            {
                LogException(ex);
            }
            finally
            {
                AvailabilityChanged?.Invoke(this, _isAvailable);
            }
        });
    }

    public virtual void Dispose()
    {
        _eventsCts.Cancel();
        _dataCts.Cancel();

        _channel.ShutdownAsync().Wait();

        GC.SuppressFinalize(this);
    }

    #region Internal

    protected readonly Channel _channel;
    protected readonly CancellationTokenSource _dataCts = new();
    protected readonly CancellationTokenSource _eventsCts = new();

    protected bool _isAvailable = false;


    protected T _client = null!; // will be initialized in the derived class constructor
    protected AsyncServerStreamingCall<U>? _eventsCall;

    // Next variables may be used in derived classes to track the state of the client/device
    protected bool _isConnected = false;
    protected bool _isStreamingData = false;
    protected bool _isLogging = false;

    protected abstract void Initialize();
    protected abstract IAsyncStreamReader<U> GetEventStream();
    protected abstract bool HandleEvent(U evt);

    protected void LogException(Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"[{GetType().Name}] Exception occurred:");
        System.Diagnostics.Debug.WriteLine(ex.Message);
    }

    protected async Task ReadEvents()
    {
        try
        {
            var responseStream = GetEventStream();

            while (await responseStream.MoveNext(_eventsCts.Token))
            {
                if (_eventsCts.IsCancellationRequested)
                    break;

                var evt = responseStream.Current;
                if (!HandleEvent(evt))
                    System.Diagnostics.Debug.WriteLine(
                        $"[{GetType().Name}] Event '{evt}' was not handled");
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