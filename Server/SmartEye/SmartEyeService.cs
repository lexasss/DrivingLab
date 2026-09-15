using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Server.Tools;
using Proto = global::SmartEye;

namespace Server.SmartEye;

internal class StreamDeckService : Proto.Dispatcher.DispatcherBase, ITelemetryService
{
    public bool IsAvailable() => _seClient != null;

    public StreamDeckService(ILoggerFactory loggerFactory) : base()
    {
        _logger = loggerFactory.CreateLogger("SEYE");

        try
        {
            SmartEyeTools.Options.Load(SE_CLIENT_OPTIONS_FILENAME);

            _seClient = new SmartEyeTools.Client();
            /* if Resquested set is not empty, then samples will not arrive
            _seClient.Requested.Append(SmartEyeTools.Data.Id.ClosestWorldIntersection);
            _seClient.RequestAvailable += (s, e) =>
            {
                object? obj = e.GetValueOrDefault(SmartEyeTools.Data.Id.ClosestWorldIntersection);
                if (obj is SmartEyeTools.WorldIntersection intersection)
                {
                    Console.WriteLine($"Intersection = {intersection.ObjectName.AsString}");
                }
            };*/

            _isActive = true;

            _logger.LogInformation("Running");
        }
        catch (Exception ex)
        {
            _logger.LogError("Cannot start the service ({ex})", ex.Message);
        }
    }

    public void Dispose()
    {
        _isActive = false;

        _seClient?.Dispose();
        _fileLogger.Dispose();

        _logger.LogInformation("Disposed");

        GC.SuppressFinalize(this);
    }

    public override Task<Common.Bool> IsAvailable (Empty request, ServerCallContext context)
    {
        return Task.FromResult(new Common.Bool { Value = IsAvailable() });
    }

    public override Task<Common.Bool> IsConnected(Empty request, ServerCallContext context)
    {
        return Task.FromResult(new Common.Bool { Value = _isConnected });
    }

    public override async Task<Common.Bool> Configure(Proto.Configuration request, ServerCallContext context)
    {
        if (!_isConnected && _seClient != null)
        {
            SmartEyeTools.Options.Instance.IntersectionSource = (SmartEyeTools.IntersectionSource)request.IntersectionSource;
            SmartEyeTools.Options.Instance.IntersectionSourceFiltered = request.UseFilteredData;

            _planeMappingMode = request.PlaneMappingMode;

            _logger.LogInformation("Configured");

            var result = await _seClient.Connect(request.Ip, request.Port);
            if (result == null)
            {
                _logger.LogInformation("Connected");
                _isConnected = true;

                _seClient.Sample += Client_Sample;
            }
            else
            {
                _logger.LogError("Failed to connect: {reason}", result.Message);
            }
        }

        return new Common.Bool() { Value = _isConnected };
    }

    public override Task<Empty> Start(Empty request, ServerCallContext context)
    {
        if (!_isSending)
        {
            _logger.LogInformation("Data streaming: started");
            _isSending = true;
        }
        return Task.FromResult(new Empty());
    }

    public override Task<Empty> Stop(Empty request, ServerCallContext context)
    {
        if (_isSending)
        {
            _logger.LogInformation("Data streaming: stopped");
            _isSending = false;
        }
        return Task.FromResult(new Empty());
    }

    public override Task<Common.Bool> SetLogFileName(Common.String request, ServerCallContext context)
    {
        return TelemetryService.SetLogFileName(request.Value, _fileLogger, _logger);
    }

    /*
    public override async Task ReadData(Empty request, IServerStreamWriter<Proto.Sample> responseStream, ServerCallContext context)
    {
        if (_isReading)
            return;

        _logger.LogInformation("Data reading: start");
        _isReading = true;

        await foreach (var data in _channel.Reader.ReadAllAsync(context.CancellationToken))
        {
            if (_isSending)
            {
                await responseStream.WriteAsync(data);
                _fileLogger.Add(data.ToStringArray());
            }
        }

        _logger.LogInformation("Data reading: stop");
        _isReading = false;
    }*/

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

    const string SE_CLIENT_OPTIONS_FILENAME = "se_client_options.json";

    readonly ILogger _logger;
    readonly SmartEyeTools.Client? _seClient;
    readonly Queue<Proto.Event> _events = [];
    readonly Tools.FileLogger _fileLogger = new();

    bool _isActive = false;
    bool _isSending = false;
    bool _isConnected = false;
    Proto.PlaneMappingMode _planeMappingMode;

    string? _currentIntersectionName = null;
    HashSet<string> _currentIntersectionNames = new();

    private void Client_Sample(object? sender, SmartEyeTools.Data.Sample e)
    {
        HandleIntersection(e);

        if (e.GazeDirection is SmartEyeTools.Vector3D gd)
        {
            _fileLogger.Add(gd.X, gd.Y, gd.Z);
        }
    }

    private void HandleIntersection(SmartEyeTools.Data.Sample sample)
    {
        if (_planeMappingMode == Proto.PlaneMappingMode.All)
            HandleAllIntersections(sample);
        else
            HandleClosestIntersection(sample);
    }

    private void HandleClosestIntersection(SmartEyeTools.Data.Sample sample)
    {
        var seClientOptions = SmartEyeTools.Options.Instance;
        var intersectionSource = (seClientOptions.IntersectionSource, seClientOptions.IntersectionSourceFiltered) switch
        {
            (SmartEyeTools.IntersectionSource.Gaze, false) => sample.ClosestWorldIntersection,
            (SmartEyeTools.IntersectionSource.Gaze, true) => sample.FilteredClosestWorldIntersection,
            (SmartEyeTools.IntersectionSource.AI, false) => sample.EstimatedClosestWorldIntersection,
            (SmartEyeTools.IntersectionSource.AI, true) => sample.FilteredEstimatedClosestWorldIntersection,
            _ => throw new Exception($"This intersection source is not implemented")
        };

        if (intersectionSource is SmartEyeTools.WorldIntersection intersection)
        {
            var intersectionName = intersection.ObjectName.AsString;
            if (_currentIntersectionName != intersectionName)
            {
                _currentIntersectionName = intersectionName;
                _logger.LogInformation("Plane {planeName}", intersection.ObjectName.AsString);
            }

            _events.Enqueue(new Proto.Event()
            {
                Intersection = new Proto.Intersection
                {
                    Name = _currentIntersectionName,
                    GazePoint = new Common.Vector()
                    {
                        X = intersection.ObjectPoint.X,
                        Y = intersection.ObjectPoint.Y,
                        Z = intersection.ObjectPoint.Z
                    }
                }
            });
        }
        else if (!string.IsNullOrEmpty(_currentIntersectionName))
        {
            _currentIntersectionName = null;

            _events.Enqueue(new Proto.Event()
            {
                Intersection = new Proto.Intersection
                {
                    Name = string.Empty,
                    GazePoint = Common.Vector.ZEROS
                }
            });
        }
    }

    private void HandleAllIntersections(SmartEyeTools.Data.Sample sample)
    {
        var seClientOptions = SmartEyeTools.Options.Instance;
        var intersectionSources = (seClientOptions.IntersectionSource, seClientOptions.IntersectionSourceFiltered) switch
        {
            (SmartEyeTools.IntersectionSource.Gaze, false) => sample.AllWorldIntersections,
            (SmartEyeTools.IntersectionSource.Gaze, true) => sample.FilteredAllWorldIntersections,
            (SmartEyeTools.IntersectionSource.AI, false) => sample.EstimatedAllWorldIntersections,
            (SmartEyeTools.IntersectionSource.AI, true) => sample.FilteredEstimatedAllWorldIntersections,
            _ => throw new Exception($"This intersection source is not implemented")
        };

        var activePlanes = new HashSet<string>();
        if (intersectionSources is SmartEyeTools.WorldIntersection[] intersections)
        {
            foreach (var intersection in intersections)
            {
                var intersectionName = intersection.ObjectName.AsString;
                activePlanes.Add(intersectionName);
            }

            var ints = new Proto.Intersections();
            ints.Items.AddRange(intersections.Select(i => new Proto.Intersection
            {
                Name = _currentIntersectionName,
                GazePoint = new Common.Vector()
                {
                    X = i.ObjectPoint.X,
                    Y = i.ObjectPoint.Y,
                    Z = i.ObjectPoint.Z
                }
            }));

            _events.Enqueue(new Proto.Event()
            {
                Intersections = ints
            });
        }

        _currentIntersectionNames.ExceptWith(activePlanes);
        _currentIntersectionNames = activePlanes;
    }

    #endregion
}
