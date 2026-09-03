using Grpc.Core;
using Microsoft.Extensions.Options;

namespace ClientExample;

public abstract class Client : IDisposable
{
    public bool IsAvailable => _isAvailable;

    public event EventHandler<bool>? AvailabilityChanged;

    public Client(IOptions<AppSettings> appSettings, int port)
    {
        _channel = new Channel(appSettings.Value.ServerIp, port, ChannelCredentials.Insecure);

        Task.Run(() =>
        {
            try
            {
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

    protected readonly Channel _channel;
    protected readonly CancellationTokenSource _dataCts = new();
    protected readonly CancellationTokenSource _eventsCts = new();

    protected bool _isAvailable = false;


    protected abstract void Initialize();

    protected static void LogException(Exception ex)
    {
        System.Diagnostics.Debug.WriteLine(ex.Message);
    }
}