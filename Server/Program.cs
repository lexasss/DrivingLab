using Grpc.Core;
using Grpc.Reflection;
using Grpc.Reflection.V1Alpha;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;
using ServiceDescriptor = Google.Protobuf.Reflection.ServiceDescriptor;

namespace Server;

class Program
{
    static Microsoft.Extensions.Logging.ILogger? _logger;

    public async static Task Main()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} - {Message:lj}{NewLine}",
                theme: _consoleTheme)
            .WriteTo.File(  
                "logs/app.log",
                rollingInterval: RollingInterval.Day,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        var serviceCollection = new ServiceCollection();

        serviceCollection.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog();
        });

        serviceCollection.AddTransient<LeapMotion.LeapMotionService>();
        serviceCollection.AddTransient<MyGaze.MyGazeService>();
        serviceCollection.AddTransient<TobiiEyeX.TobiiEyeXService>();
        serviceCollection.AddTransient<SmartEye.StreamDeckService>();
        serviceCollection.AddTransient<SoundPlayer.SoundPlayerService>();
        serviceCollection.AddTransient<Screen.ScreenService>();
        serviceCollection.AddTransient<Pointing.PointingService>();
        serviceCollection.AddTransient<TensionR.TensionRService>();
        serviceCollection.AddTransient<StreamDeck.StreamDeckService>();
        serviceCollection.AddTransient<Driving.DrivingService>();

        var serviceProvider = serviceCollection.BuildServiceProvider();

        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger("MAIN");

        var creators = new Task<(IService, Grpc.Core.Server)?>[]
        {
            Create<LeapMotion.LeapMotionService>(
                serviceProvider,
                "LeapMotion",
                (int)Common.Ports.LeapMotion,
                global::LeapMotion.Dispatcher.Descriptor,
                service => global::LeapMotion.Dispatcher.BindService(
                    (global::LeapMotion.Dispatcher.DispatcherBase)service
                )
            ),
            Create<MyGaze.MyGazeService>(
                serviceProvider,
                "MyGaze",
                (int)Common.Ports.MyGaze,
                Gaze.Dispatcher.Descriptor,
                service => Gaze.Dispatcher.BindService(
                    (Gaze.Dispatcher.DispatcherBase)service
                )
            ),
            Create<TobiiEyeX.TobiiEyeXService>(
                serviceProvider,
                "Tobii EyeX",
                (int)Common.Ports.TobiiEyeX,
                Gaze.Dispatcher.Descriptor,
                service => Gaze.Dispatcher.BindService(
                    (Gaze.Dispatcher.DispatcherBase)service
                )
            ),
            Create<SmartEye.StreamDeckService>(
                serviceProvider,
                "Smart Eye",
                (int)Common.Ports.SmartEye,
                global::SmartEye.Dispatcher.Descriptor,
                service => global::SmartEye.Dispatcher.BindService(
                    (global::SmartEye.Dispatcher.DispatcherBase)service
                )
            ),
            Create<SoundPlayer.SoundPlayerService>(
                serviceProvider,
                "Sound Player",
                (int)Common.Ports.SoundPlayer,
                global::SoundPlayer.Dispatcher.Descriptor,
                service => global::SoundPlayer.Dispatcher.BindService(
                    (global::SoundPlayer.Dispatcher.DispatcherBase)service
                )
            ),
            Create<Screen.ScreenService>(
                serviceProvider,
                "Screen",
                (int)Common.Ports.Screen,
                global::Screen.Dispatcher.Descriptor,
                service => global::Screen.Dispatcher.BindService(
                    (global::Screen.Dispatcher.DispatcherBase)service
                )
            ),
            Create<Pointing.PointingService>(
                serviceProvider,
                "Pointing",
                (int)Common.Ports.Pointing,
                global::Pointing.Dispatcher.Descriptor,
                service => global::Pointing.Dispatcher.BindService(
                    (global::Pointing.Dispatcher.DispatcherBase)service
                )
            ),
            Create<TensionR.TensionRService>(
                serviceProvider,
                "TensionR Belt",
                (int)Common.Ports.TensionR,
                global::TensionR.Dispatcher.Descriptor,
                service => global::TensionR.Dispatcher.BindService(
                    (global::TensionR.Dispatcher.DispatcherBase)service
                )
            ),
            Create<StreamDeck.StreamDeckService>(
                serviceProvider,
                "Stream Deck",
                (int)Common.Ports.StreamDeck,
                global::StreamDeck.Dispatcher.Descriptor,
                service => global::StreamDeck.Dispatcher.BindService(
                    (global::StreamDeck.Dispatcher.DispatcherBase)service
                )
            ),
            Create<Driving.DrivingService>(
                serviceProvider,
                "Driving",
                (int)Common.Ports.Driving,
                global::Driving.Dispatcher.Descriptor,
                service => global::Driving.Dispatcher.BindService(
                    (global::Driving.Dispatcher.DispatcherBase)service
                )
            ),
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
            server.ShutdownAsync()
        ));
    }

    private static async Task<(IService, Grpc.Core.Server)?> Create<T>(
        IServiceProvider serviceProvider,
        string name,
        int port,
        ServiceDescriptor descriptor,
        Func<IService, ServerServiceDefinition> getBoundService) 
            where T : IService
    {
        IService service = await Task.Run(() => 
            serviceProvider.GetRequiredService<T>()
        );

        if (service.IsAvailable())
        {
            var reflectionServiceImpl = new ReflectionServiceImpl(
                descriptor,
                ServerReflection.Descriptor
            );
            var server = new Grpc.Core.Server
            {
                Services = {
                    getBoundService(service),
                    ServerReflection.BindService(reflectionServiceImpl)
                },
                Ports = {
                    new ServerPort(
                        "0.0.0.0",
                        port,
                        ServerCredentials.Insecure
                    )
                }
            };
            server.Start();

            _logger?.LogInformation("{name} server is listening on port {port}",
                name, port);

            return (service, server);
        }
        else
        {
            service.Dispose();
        }

        return null;
    }

    static readonly SystemConsoleTheme _consoleTheme = new(
        new Dictionary<ConsoleThemeStyle, SystemConsoleThemeStyle>()
        {
            [ConsoleThemeStyle.Text] = new SystemConsoleThemeStyle {
                Foreground = ConsoleColor.White
            },
            [ConsoleThemeStyle.SecondaryText] = new SystemConsoleThemeStyle {
                Foreground = ConsoleColor.Gray
            },
            [ConsoleThemeStyle.TertiaryText] = new SystemConsoleThemeStyle {
                Foreground = ConsoleColor.DarkGray
            },

            [ConsoleThemeStyle.Invalid] = new SystemConsoleThemeStyle {
                Foreground = ConsoleColor.Yellow
            },

            [ConsoleThemeStyle.Null] = new SystemConsoleThemeStyle {
                Foreground = ConsoleColor.Blue
            },
            [ConsoleThemeStyle.Name] = new SystemConsoleThemeStyle {
                Foreground = ConsoleColor.Gray
            },
            [ConsoleThemeStyle.String] = new SystemConsoleThemeStyle {
                Foreground = ConsoleColor.Cyan
            },
            [ConsoleThemeStyle.Number] = new SystemConsoleThemeStyle {
                Foreground = ConsoleColor.Green
            },
            [ConsoleThemeStyle.Boolean] = new SystemConsoleThemeStyle {
                Foreground = ConsoleColor.Blue
            },
            [ConsoleThemeStyle.Scalar] = new SystemConsoleThemeStyle {
                Foreground = ConsoleColor.DarkBlue
            },

            [ConsoleThemeStyle.LevelVerbose] = new SystemConsoleThemeStyle {
                Foreground = ConsoleColor.Gray
            },
            [ConsoleThemeStyle.LevelDebug] = new SystemConsoleThemeStyle {
                Foreground = ConsoleColor.Gray
            },
            [ConsoleThemeStyle.LevelInformation] = new SystemConsoleThemeStyle {
                Foreground = ConsoleColor.White
            },
            [ConsoleThemeStyle.LevelWarning] = new SystemConsoleThemeStyle {
                Foreground = ConsoleColor.Yellow
            },
            [ConsoleThemeStyle.LevelError] = new SystemConsoleThemeStyle {
                Foreground = ConsoleColor.Red
            },
            [ConsoleThemeStyle.LevelFatal] = new SystemConsoleThemeStyle {
                Foreground = ConsoleColor.White,
                Background = ConsoleColor.Red
            },
        }
    );
}
