using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Scrobblint.Application.Abstractions;
using Scrobblint.Application.Abstractions.Persistence;
using Scrobblint.Application.Abstractions.Security;
using Scrobblint.Infrastructure.Configuration;
using Scrobblint.Infrastructure.Persistence;
using Scrobblint.Infrastructure.Persistence.Providers;
using Scrobblint.Infrastructure.Persistence.Repositories;
using Scrobblint.Infrastructure.Security;
using Scrobblint.Infrastructure.Time;

namespace Scrobblint.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers persistence (provider chosen from <c>Database:Provider</c>), security primitives
    /// and the system clock. Pair this with <c>AddApplicationServices()</c>.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services.Configure<SeedOptions>(configuration.GetSection(SeedOptions.SectionName));

        var dbOptions = configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new DatabaseOptions();
        var provider = DataStorageProviderFactory.Create(dbOptions);
        var connectionString = DataStorageProviderFactory.ResolveConnectionString(dbOptions);

        // The chosen provider is available to the rest of the app via both abstractions.
        services.AddSingleton<IEfDataStorageProvider>(provider);
        services.AddSingleton<IDataStorageProvider>(provider);

        services.AddDbContext<ScrobblintDbContext>(options => provider.Configure(options, connectionString));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IScrobbleRepository, ScrobbleRepository>();
        services.AddScoped<IUserSettingsRepository, UserSettingsRepository>();

        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<ITokenGenerator, TokenGenerator>();
        services.AddSingleton<IClock, SystemClock>();

        return services;
    }
}
