using Microsoft.EntityFrameworkCore;
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

        // Register the context through a pooled factory rather than AddDbContext. A single
        // DI-scoped context is fragile in a Blazor host: it can be shared across overlapping
        // renders, and a long-lived scope leaves a wide window for one query to be torn down
        // (e.g. by request cancellation) while another starts — surfacing as "a second operation
        // was started on this context instance". The factory owns context creation; the scoped
        // bridge below leases exactly one context per request/operation so the existing repository
        // + unit-of-work pattern (all repositories and SaveChanges must share one context for a
        // transactional write) keeps working unchanged. Components that need their own short-lived
        // context can inject IDbContextFactory<ScrobblintDbContext> directly.
        services.AddPooledDbContextFactory<ScrobblintDbContext>((sp, options) =>
        {
            var dbOptions = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            var provider = sp.GetRequiredService<IEfDataStorageProvider>();
            provider.Configure(options, DataStorageProviderFactory.ResolveConnectionString(dbOptions));
        });

        // One pooled context per scope, returned to the pool when the scope is disposed.
        services.AddScoped<ScrobblintDbContext>(sp =>
            sp.GetRequiredService<IDbContextFactory<ScrobblintDbContext>>().CreateDbContext());

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
        })
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            EnableMultipleHttp2Connections = true,
            SslOptions = new System.Net.Security.SslClientAuthenticationOptions
            {
                EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13
            }
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
