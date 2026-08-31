using Grpc.Core;
using Grpc.Reflection;
using Grpc.Reflection.V1Alpha;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Server;

class Program
{
    static ILogger<Program>? _logger;

    public async static Task Main()
    {
        var serviceCollection = new ServiceCollection();

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .WriteTo.File(
                "logs/app.log",
                rollingInterval: RollingInterval.Day)
            .CreateLogger();

        serviceCollection.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog();
        });

        serviceCollection.AddTransient<MyGaze.MyGazeService>();
        serviceCollection.AddTransient<TobiiEyeX.TobiiEyeXService>();
        serviceCollection.AddTransient<LeapMotion.LeapMotionService>();

        var serviceProvider = serviceCollection.BuildServiceProvider();

        _logger = serviceProvider.GetRequiredService<ILogger<Program>>();

        var leapMotionService = serviceProvider.GetRequiredService<LeapMotion.LeapMotionService>();
        var myGazeService = serviceProvider.GetRequiredService<MyGaze.MyGazeService>();
        var tobiiEyeXService = serviceProvider.GetRequiredService<TobiiEyeX.TobiiEyeXService>();

        List<IDisposable> services = [];
        List<Grpc.Core.Server?> servers = [];
        List<Task> creators = new List<Task>();

        if (leapMotionService.IsAvailable())
        {
            creators.Add(Task.Run(() =>
            {
                var leapMotionServer = CreateServer("LeapMotion",
                    leapMotionService,
                    global::LeapMotion.Dispatcher.Descriptor,
                    global::LeapMotion.Dispatcher.BindService(leapMotionService),
                    (int)Common.Ports.LeapMotion
                );
                servers.Add(leapMotionServer);
                services.Add(leapMotionService);
            }));
        }

        if (myGazeService.IsAvailable())
        {
            creators.Add(Task.Run(() =>
            {
                var myGazeServer = CreateServer("MyGaze",
                    myGazeService,
                    Gaze.Dispatcher.Descriptor,
                    Gaze.Dispatcher.BindService(myGazeService),
                    (int)Common.Ports.MyGaze
                );
                servers.Add(myGazeServer);
                services.Add(myGazeService);
            }));
        }

        if (tobiiEyeXService.IsAvailable())
        {
            creators.Add(Task.Run(() =>
            {
                var tobiiEyeXServer = CreateServer("Tobii EyeX",
                    tobiiEyeXService,
                    Gaze.Dispatcher.Descriptor,
                    Gaze.Dispatcher.BindService(tobiiEyeXService),
                    (int)Common.Ports.TobiiEyeX
                );
                servers.Add(tobiiEyeXServer);
                services.Add(tobiiEyeXService);
            }));
        }

        Task.WaitAll(creators);

        Console.WriteLine("Press any key to stop the server...");
        Console.ReadKey();

        foreach (var service in services)
            service.Dispose();

        Task.WaitAll(servers.Select(server =>
            server!.ShutdownAsync()
        ));
    }

    private static Grpc.Core.Server CreateServer(
        string name,
        IService service,
        Google.Protobuf.Reflection.ServiceDescriptor descriptor,
        ServerServiceDefinition serviceDefinition,
        int port)
    {
        var reflectionServiceImpl = new ReflectionServiceImpl(descriptor, ServerReflection.Descriptor);
        var server = new Grpc.Core.Server
        {
            Services = { serviceDefinition, ServerReflection.BindService(reflectionServiceImpl) },
            Ports = { new ServerPort("0.0.0.0", port, ServerCredentials.Insecure) }
        };
        server.Start();

        _logger?.LogInformation($"[APP] {name} server is listening on port {port}");

        return server;
    }
}
