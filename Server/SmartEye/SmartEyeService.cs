using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Proto = global::SmartEye;

namespace Server.SmartEye;

internal class StreamDeckService :
    Proto.Dispatcher.DispatcherBase,
    ITelemetryService
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

            _baseService = new(_logger);
        }
        catch (Exception ex)
        {
            _logger.LogError("Cannot start the service ({ex})", ex.Message);
        }
    }

    public void Dispose()
    {
        _seClient?.Dispose();
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

    public override async Task<Common.Bool> Configure(
        Proto.Configuration request,
        ServerCallContext context)
    {
        if (!_isConnected && _seClient != null)
        {
            SmartEyeTools.Options.Instance.IntersectionSource =
                (SmartEyeTools.IntersectionSource)request.IntersectionSource;
            SmartEyeTools.Options.Instance.IntersectionSourceFiltered =
                request.UseFilteredData;

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

        return new Common.Bool(_isConnected);
    }

    public override Task<Empty> Start(
        Empty request,
        ServerCallContext context)
    {
        _baseService?.Start();
        return Common.Constants.Empty;
    }

    public override Task<Empty> Stop(
        Empty request,
        ServerCallContext context)
    {
        _baseService?.Stop();
        return Common.Constants.Empty;
    }

    public override Task<Common.Bool> SetLogFileName(
        Common.String request,
        ServerCallContext context)
    {
        if (_baseService == null)
            return Common.Bool.False;

        return _baseService.SetLogFileName(request.Value);
    }

    public override async Task ReadData(
        Empty request,
        IServerStreamWriter<Common.Vector> responseStream,
        ServerCallContext context)
    {
        if (_baseService == null)
            return;

        await _baseService.ReadData(request, responseStream, context, 3);
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

    const string SE_CLIENT_OPTIONS_FILENAME = "se_client_options.json";

    readonly ILogger _logger;
    readonly SmartEyeTools.Client? _seClient;
    readonly Tools.TelemetryService<Common.Vector, Proto.Event>? _baseService;

    bool _isConnected = false;

    Proto.PlaneMappingMode _planeMappingMode;
    string? _currentIntersectionName = null;
    HashSet<string> _currentIntersectionNames = [];

    private void Client_Sample(object? sender, SmartEyeTools.Data.Sample e)
    {
        HandleIntersection(e);

        if (e.GazeDirection is SmartEyeTools.Vector3D gd)
        {
            _baseService?.Publish(new Common.Vector()
            {
                X = gd.X,
                Y = gd.Y,
                Z = gd.Z
            });
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
        var source = seClientOptions.IntersectionSource;
        var isFiltered = seClientOptions.IntersectionSourceFiltered;
        
        var intersectionSource = (source, isFiltered) switch
        {
            (SmartEyeTools.IntersectionSource.Gaze, false) => 
                sample.ClosestWorldIntersection,
            (SmartEyeTools.IntersectionSource.Gaze, true) =>
                sample.FilteredClosestWorldIntersection,
            (SmartEyeTools.IntersectionSource.AI, false) =>
                sample.EstimatedClosestWorldIntersection,
            (SmartEyeTools.IntersectionSource.AI, true) =>
                sample.FilteredEstimatedClosestWorldIntersection,
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

            _baseService?.Publish(new Proto.Event()
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

            _baseService?.Publish(new Proto.Event()
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
        var source = seClientOptions.IntersectionSource;
        var isFiltered = seClientOptions.IntersectionSourceFiltered;

        var intersectionSources = (source, isFiltered) switch
        {
            (SmartEyeTools.IntersectionSource.Gaze, false) =>
                sample.AllWorldIntersections,
            (SmartEyeTools.IntersectionSource.Gaze, true) =>
                sample.FilteredAllWorldIntersections,
            (SmartEyeTools.IntersectionSource.AI, false) =>
                sample.EstimatedAllWorldIntersections,
            (SmartEyeTools.IntersectionSource.AI, true) =>
                sample.FilteredEstimatedAllWorldIntersections,
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

            var grpcIntersections = new Proto.Intersections();
            grpcIntersections.Items.AddRange(
                intersections.Select(i => new Proto.Intersection
                {
                    Name = _currentIntersectionName,
                    GazePoint = new Common.Vector()
                    {
                        X = i.ObjectPoint.X,
                        Y = i.ObjectPoint.Y,
                        Z = i.ObjectPoint.Z
                    }
                })
            );

            _baseService?.Publish(new Proto.Event()
            {
                Intersections = grpcIntersections
            });
        }

        _currentIntersectionNames.ExceptWith(activePlanes);
        _currentIntersectionNames = activePlanes;
    }

    #endregion
}
