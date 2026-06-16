using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Scrobblint.Application.Services;

namespace Scrobblint.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the application services (business logic). Persistence and security
    /// implementations are supplied separately by the Infrastructure layer.
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddScoped<AuthService>();
        services.AddScoped<ScrobbleService>();
        services.AddScoped<StatisticsService>();
        services.AddScoped<UserService>();
        services.AddScoped<ExternalConnectionService>();
        services.AddScoped<ScrobbleImportService>();

        services.AddScoped<IAuthService>(sp => sp.GetRequiredService<AuthService>());
        services.AddScoped<IScrobbleService>(sp => sp.GetRequiredService<ScrobbleService>());
        services.AddScoped<IStatisticsService>(sp =>
            new CachedStatisticsService(
                sp.GetRequiredService<StatisticsService>(),
                sp.GetRequiredService<IMemoryCache>()));
        services.AddScoped<IUserService>(sp => sp.GetRequiredService<UserService>());
        services.AddScoped<IExternalConnectionService>(sp => sp.GetRequiredService<ExternalConnectionService>());
        services.AddScoped<IScrobbleImportService>(sp => sp.GetRequiredService<ScrobbleImportService>());
        return services;
    }
}
