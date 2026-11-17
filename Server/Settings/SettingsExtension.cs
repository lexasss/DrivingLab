using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Server.Settings;

public static class SettingsExtensions
{
    public static IServiceCollection AddAppSettings(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<Settings>()
            .Bind(configuration)
            .ValidateOnStart();

       // Register the root Settings object
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<Settings>>().Value);

        // Register individual settings classes
        services.AddSingleton(sp => sp.GetRequiredService<Settings>().Carla);

        return services;
    }
}