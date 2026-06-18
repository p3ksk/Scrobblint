using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Scrobblint.Application.Abstractions;
using Scrobblint.Application.Abstractions.CoverArt;
using Scrobblint.Application.Abstractions.Persistence;
using Scrobblint.Application.Abstractions.Relay;
using Scrobblint.Application.Abstractions.Security;
using Scrobblint.Infrastructure.Configuration;
using Scrobblint.Infrastructure.CoverArt;
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

        // Register a (non-pooled) context factory plus a scoped bridge. Pooling must NOT be used
        // here: when a Blazor request is aborted (enhanced navigation cancels the page you just
        // left), its query keeps running on CancellationToken.None while the DI scope is torn down
        // and the context returned to the pool. The very next request then rents that same instance
        // and runs its own query on it concurrently — "a second operation was started on this
        // context instance". A non-pooled factory hands every scope a brand-new context that is
        // never reused, so an abandoned request can only ever touch its own (disposed) context and
        // can't corrupt the next one. The scoped bridge keeps the repository + unit-of-work pattern
        // (all repositories and SaveChanges share one context per request) working unchanged.
        services.AddDbContextFactory<ScrobblintDbContext>((sp, options) =>
        {
            var dbOptions = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            var provider = sp.GetRequiredService<IEfDataStorageProvider>();
            provider.Configure(options, DataStorageProviderFactory.ResolveConnectionString(dbOptions));
        });

        // One fresh context per scope, disposed (not pooled) when the scope ends.
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

        // --- Cover art (Deezer API, free — no key required) ---
        services.AddHttpClient(DeezerCoverArtProvider.HttpClientName, client =>
        {
            client.BaseAddress = new Uri("https://api.deezer.com/");
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Scrobblint/1.0 (+https://github.com/scrobblint)");
        });
        services.AddSingleton<ICoverArtProvider, DeezerCoverArtProvider>();

        // --- History import (Last.fm) ---
        services.AddSingleton<IScrobbleImportQueue, ScrobbleImportQueue>();
        services.AddHostedService<ScrobbleImportWorker>();

        return services;
    }
}
