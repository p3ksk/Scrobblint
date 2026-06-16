using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Scrobblint.Application.Abstractions;
using Scrobblint.Application.Abstractions.Security;
using Scrobblint.Domain.Entities;
using Scrobblint.Infrastructure.Configuration;
using Scrobblint.Infrastructure.Persistence.Providers;

namespace Scrobblint.Infrastructure.Persistence;

/// <summary>
/// Applies migrations and (optionally) seeds the first administrator account on start-up.
/// Call <see cref="InitializeAsync"/> once during application boot.
/// </summary>
public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInitializer");
        var context = sp.GetRequiredService<ScrobblintDbContext>();
        var provider = sp.GetRequiredService<IEfDataStorageProvider>();
        var dbOptions = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;

        if (dbOptions.ApplyMigrationsOnStartup && provider.SupportsMigrations)
        {
            logger.LogInformation("Applying database migrations ({Provider})…", provider.Name);
            await context.Database.MigrateAsync(cancellationToken);
        }

        await SeedAdminAsync(sp, context, logger, cancellationToken);
    }

    private static async Task SeedAdminAsync(
        IServiceProvider sp, ScrobblintDbContext context, ILogger logger, CancellationToken cancellationToken)
    {
        var seed = sp.GetRequiredService<IOptions<SeedOptions>>().Value;
        if (!seed.Enabled)
            return;

        if (await context.Users.AnyAsync(u => u.IsAdmin, cancellationToken))
        {
            logger.LogInformation("Admin seeding skipped: an administrator already exists.");
            return;
        }

        var username = seed.Username.Trim();
        if (await context.Users.AnyAsync(u => u.Username == username, cancellationToken))
        {
            logger.LogWarning("Admin seeding skipped: username '{Username}' is already taken.", username);
            return;
        }

        var hasher = sp.GetRequiredService<IPasswordHasher>();
        var tokens = sp.GetRequiredService<ITokenGenerator>();
        var clock = sp.GetRequiredService<IClock>();

        var admin = new User
        {
            Username = username,
            Email = seed.Email.Trim(),
            PasswordHash = hasher.Hash(seed.Password),
            ApiToken = tokens.GenerateApiToken(),
            CreatedAt = clock.UtcNow,
            IsAdmin = true,
            IsDisabled = false
        };
        admin.Settings = new UserSettings { UserId = admin.Id };

        context.Users.Add(admin);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seeded administrator account '{Username}'. Change the password after first login!", username);
    }
}
