using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Windows;

namespace ClientExample;

public partial class App : Application
{
    IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var builder = Host.CreateApplicationBuilder();

        ConfigureServices(builder.Services, builder.Configuration);

        _host = builder.Build();
        await _host.StartAsync();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        base.OnExit(e);
    }

    private static void ConfigureServices(IServiceCollection services, ConfigurationManager config)
    {
        services.Configure<AppSettings>(config);

        services.AddSingleton<LeapMotionClient>();
        services.AddSingleton<SmartEyeClient>();
        services.AddSingleton<SoundPlayerClient>();
        services.AddSingleton<ScreenClient>();

        services.AddTransient<LeapMotionViewModel>();
        services.AddTransient<SmartEyeViewModel>();
        services.AddTransient<SoundPlayerViewModel>();
        services.AddTransient<ScreenViewModel>();

        services.AddTransient<MainViewModel>();
        services.AddTransient<MainWindow>();
    }
}
