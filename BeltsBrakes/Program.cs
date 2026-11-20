using BeltsBrakes.Controllers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace BeltsBrakes;

internal class Program
{
    static ILogger<Program>? _logger;

    static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.None);
        builder.Logging.AddConsoleFormatter<PlainConsoleFormatter, ConsoleFormatterOptions>();
        builder.Logging.AddConsole(options =>
        {
            options.FormatterName = "plain";
        });

        ConfigureServices(builder.Services, builder.Configuration);

        var host = builder.Build();
        await host.StartAsync();

        _logger = host.Services.GetRequiredService<ILogger<Program>>();

        Console.WriteLine("Belts & Brakes is running");

        Communicator communicator;

        try
        {
            communicator = host.Services.GetRequiredService<Communicator>();
            if (!SetBeltComPort(communicator))
                return;

            _logger.LogInformation("Prepare to calibrate the belts, then press any key...");
            Console.ReadKey(true);

            if (!communicator.PrepareBelt())
            {
                _logger.LogError("Failed to prepare the system");
                return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to proceed:");
            return;
        }

        var beltController = host.Services.GetRequiredService<BeltController>();
        var speedWarnSignController = host.Services.GetRequiredService<SpeedWarnSignController>();
        var carHornController = host.Services.GetRequiredService<CarHornController>();

        communicator.Start();

        _logger.LogInformation("Press any key to stop...");
        Console.ReadKey(true);

        communicator.Stop();
    }

    private static void ConfigureServices(
        IServiceCollection services,
        ConfigurationManager config)
    {
        services.Configure<AppSettings>(config);

        services.AddSingleton<TensionRClient>();
        services.AddSingleton<CarlaClient>();
        services.AddSingleton<ScreenClient>();
        services.AddSingleton<WheelAndPedalsClient>();
        services.AddSingleton<SoundPlayerClient>();

        services.AddSingleton<Communicator>();

        services.AddSingleton<BeltController>();
        services.AddSingleton<SpeedWarnSignController>();
        services.AddSingleton<CarHornController>();
    }

    private static bool SetBeltComPort(Communicator comm)
    {
        Console.WriteLine("COM ports:");
        foreach (var port in comm.ComPorts)
        {
            Console.WriteLine($"  - {port.Name}: {port.Description}");
        }
        Console.Write($"Belt COM port: {comm.BeltComPort?.Name ?? ""}");
        var input = Console.ReadLine()?.Trim();
        if (comm.BeltComPort != null && string.IsNullOrEmpty(input))
        {
            // OK, use default port
        }
        else if (!string.IsNullOrEmpty(input))
        {
            input = input.Trim().ToUpper();
            string comPortName = input.StartsWith("COM", StringComparison.CurrentCultureIgnoreCase) ? input : $"COM{input}";
            var port = comm.ComPorts.FirstOrDefault(p => p.Name == comPortName);
            if (port != null)
            {
                comm.BeltComPort = port;
            }
            else
            {
                _logger?.LogError($"Port {comPortName} not found");
                return false;
            }
        }
        else
        {
            _logger?.LogWarning("No COM port selected");
            return false;
        }

        return true;
    }
}
