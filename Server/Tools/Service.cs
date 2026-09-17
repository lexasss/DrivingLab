using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace Server.Tools;

internal class Service<T> : IDisposable
{
    public bool IsActive => _isActive;

    public Service(ILogger logger)
    {
        _logger = logger;
    }

    public void Publish(T evt)
    {
        _events.Enqueue(evt);
    }

    public async Task ReadEvents(
        Empty request,
        IServerStreamWriter<T> responseStream,
        ServerCallContext context)
    {
        while (_isActive && !context.CancellationToken.IsCancellationRequested)
        {
            await Task.Delay(Constants.EVENT_CHECK_INTERVAL);

            if (_events.Count > 0)
            {
                var evt = _events.Dequeue();
                await responseStream.WriteAsync(evt);
            }
        }
    }

    public virtual void Dispose()
    {
        _isActive = false;

        _logger.LogInformation("Disposed");
    }

    #region Internal

    protected readonly ILogger _logger;
    readonly Queue<T> _events = [];

    bool _isActive = true;

    #endregion
}
