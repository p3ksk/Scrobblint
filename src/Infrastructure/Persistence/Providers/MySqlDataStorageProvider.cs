using Microsoft.EntityFrameworkCore;

namespace Scrobblint.Infrastructure.Persistence.Providers;

/// <summary>MySQL / MariaDB storage provider (via Pomelo).</summary>
public sealed class MySqlDataStorageProvider : IEfDataStorageProvider
{
    /// <summary>Assembly that holds the MySQL-specific EF migrations.</summary>
    public const string MigrationsAssemblyName = "Scrobblint.Migrations.MySql";

    // A fixed server version keeps start-up and design-time deterministic (no AutoDetect round-trip).
    // Override via Database:ServerVersion if you target a different MySQL/MariaDB release.
    public static readonly Version DefaultServerVersion = new(8, 4, 0);

    private readonly Version _serverVersion;

    public MySqlDataStorageProvider(Version? serverVersion = null) =>
        _serverVersion = serverVersion ?? DefaultServerVersion;

    public string Name => "MySQL";
    public bool SupportsMigrations => true;

    public void Configure(DbContextOptionsBuilder options, string connectionString)
    {
        var version = new MySqlServerVersion(_serverVersion);
        options.UseMySql(connectionString, version, mysql =>
        {
            mysql.MigrationsAssembly(MigrationsAssemblyName);
            // NOTE: deliberately no EnableRetryOnFailure. The retrying execution strategy treats a
            // connection torn down by request cancellation (Blazor enhanced-navigation aborts the
            // in-flight request) as a transient fault and retries on the same DbContext while the
            // cancelled operation is still tearing down — producing "a second operation was started
            // on this context instance" and leaving MySqlConnector sessions/pooled connections in a
            // broken state that then fails subsequent requests. A failed SSR render is recoverable by
            // reload, so the retry strategy costs more than it buys for a co-located self-hosted DB.
        });
    }
}
