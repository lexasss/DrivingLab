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

        serviceCollection.AddTransient<LeapMotion.LeapMotionService>();
        serviceCollection.AddTransient<MyGaze.MyGazeService>();
        serviceCollection.AddTransient<TobiiEyeX.TobiiEyeXService>();
        serviceCollection.AddTransient<SmartEye.SmartEyeService>();
        serviceCollection.AddTransient<SoundPlayer.SoundPlayerService>();

        var serviceProvider = serviceCollection.BuildServiceProvider();

        _logger = serviceProvider.GetRequiredService<ILogger<Program>>();

        var creators = new Task<(IService, Grpc.Core.Server)?>[]
        {
            Create<LeapMotion.LeapMotionService>(serviceProvider,
                "LeapMotion", (int)Common.Ports.LeapMotion,
                global::LeapMotion.Dispatcher.Descriptor,
                service => global::LeapMotion.Dispatcher.BindService((global::LeapMotion.Dispatcher.DispatcherBase)service)
            ),
            Create<MyGaze.MyGazeService>(serviceProvider,
                "MyGaze", (int)Common.Ports.MyGaze,
                Gaze.Dispatcher.Descriptor,
                service => Gaze.Dispatcher.BindService((Gaze.Dispatcher.DispatcherBase)service)
            ),
            Create<TobiiEyeX.TobiiEyeXService>(serviceProvider,
                "Tobii EyeX", (int)Common.Ports.TobiiEyeX,
                Gaze.Dispatcher.Descriptor,
                service => Gaze.Dispatcher.BindService((Gaze.Dispatcher.DispatcherBase)service)
            ),
            Create<SmartEye.SmartEyeService>(serviceProvider,
                "Smart Eye", (int)Common.Ports.SmartEye,
                global::SmartEye.Dispatcher.Descriptor,
                service => global::SmartEye.Dispatcher.BindService((global::SmartEye.Dispatcher.DispatcherBase)service)
            ),
            Create<SoundPlayer.SoundPlayerService>(serviceProvider,
                "Sound Player", (int)Common.Ports.SoundPlayer,
                global::SoundPlayer.Dispatcher.Descriptor,
                service => global::SoundPlayer.Dispatcher.BindService((global::SoundPlayer.Dispatcher.DispatcherBase)service)
            )
        };

        Task.WaitAll(creators);

        List<IService> services = [];
        List<Grpc.Core.Server> servers = [];
        foreach (var creator in creators)
        {
            if (creator.Result != null)
            {
                services.Add(creator.Result.Value.Item1);
                servers.Add(creator.Result.Value.Item2);
            }
        }

        Console.WriteLine("Press any key to stop the server...");
        Console.ReadKey(true);

        foreach (var service in services)
            service.Dispose();

        Task.WaitAll(servers.Select(server =>
            server!.ShutdownAsync()
        ));
    }

    private static async Task<(IService, Grpc.Core.Server)?> Create<T>(
        IServiceProvider serviceProvider,
        string name,
        int port,
        Google.Protobuf.Reflection.ServiceDescriptor descriptor,
        Func<IService, ServerServiceDefinition> getBoundService) 
        where T : IService
    {
        IService service = await Task.Run(() => serviceProvider.GetRequiredService<T>());
        if (service.IsAvailable())
        {
            var reflectionServiceImpl = new ReflectionServiceImpl(descriptor, ServerReflection.Descriptor);
            var server = new Grpc.Core.Server
            {
                Services = { getBoundService(service), ServerReflection.BindService(reflectionServiceImpl) },
                Ports = { new ServerPort("0.0.0.0", port, ServerCredentials.Insecure) }
            };
            server.Start();

            _logger?.LogInformation("[APP] {name} server is listening on port {port}", name, port);

            return (service, server);
        }
        else
        {
            service.Dispose();
        }

        return null;
    }
}
