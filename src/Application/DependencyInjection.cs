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
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IScrobbleService, ScrobbleService>();
        services.AddScoped<IStatisticsService, StatisticsService>();
        services.AddScoped<IUserService, UserService>();
        return services;
    }
}
