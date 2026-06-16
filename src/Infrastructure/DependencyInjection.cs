using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Scrobblint.Application.Abstractions;
using Scrobblint.Application.Abstractions.Persistence;
using Scrobblint.Application.Abstractions.Relay;
using Scrobblint.Application.Abstractions.Security;
using Scrobblint.Infrastructure.Configuration;
using Scrobblint.Infrastructure.Import;
using Scrobblint.Infrastructure.Persistence;
using Scrobblint.Infrastructure.Persistence.Providers;
using Scrobblint.Infrastructure.Persistence.Repositories;
using Scrobblint.Infrastructure.Relay;
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
        services.Configure<LastfmOptions>(configuration.GetSection(LastfmOptions.SectionName));
        services.Configure<ImportOptions>(configuration.GetSection(ImportOptions.SectionName));

        // Resolve the provider and connection lazily through the options system so configuration added
        // late (e.g. by WebApplicationFactory in tests, or env vars) is honoured.
        services.AddSingleton<IEfDataStorageProvider>(sp =>
            DataStorageProviderFactory.Create(sp.GetRequiredService<IOptions<DatabaseOptions>>().Value));
        services.AddSingleton<IDataStorageProvider>(sp => sp.GetRequiredService<IEfDataStorageProvider>());

        services.AddDbContext<ScrobblintDbContext>((sp, options) =>
        {
            var dbOptions = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            var provider = sp.GetRequiredService<IEfDataStorageProvider>();
            provider.Configure(options, DataStorageProviderFactory.ResolveConnectionString(dbOptions));
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IScrobbleRepository, ScrobbleRepository>();
        services.AddScoped<IUserSettingsRepository, UserSettingsRepository>();
        services.AddScoped<IExternalConnectionRepository, ExternalConnectionRepository>();
        services.AddScoped<IScrobbleImportRepository, ScrobbleImportRepository>();

        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<ITokenGenerator, TokenGenerator>();
        services.AddSingleton<IClock, SystemClock>();

        // --- Scrobble relaying to external services (Last.fm, ListenBrainz) ---
        services.AddHttpClient(RelayHttpClient.Name, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(20);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Scrobblint/1.0 (+https://github.com/scrobblint)");
        });

        services.AddSingleton<ListenBrainzRelay>();
        services.AddSingleton<LastfmRelay>();
        services.AddSingleton<IListenBrainzRelay>(sp => sp.GetRequiredService<ListenBrainzRelay>());
        services.AddSingleton<ILastfmRelay>(sp => sp.GetRequiredService<LastfmRelay>());
        services.AddSingleton<IScrobbleRelay>(sp => sp.GetRequiredService<ListenBrainzRelay>());
        services.AddSingleton<IScrobbleRelay>(sp => sp.GetRequiredService<LastfmRelay>());

        services.AddSingleton<IScrobbleRelayQueue, ScrobbleRelayQueue>();
        services.AddHostedService<ScrobbleRelayDispatcher>();

        // --- History import (Last.fm) ---
        services.AddSingleton<IScrobbleImportQueue, ScrobbleImportQueue>();
        services.AddHostedService<ScrobbleImportWorker>();

        return services;
    }
}
