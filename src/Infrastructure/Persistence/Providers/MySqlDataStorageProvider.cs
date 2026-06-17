using Microsoft.EntityFrameworkCore;

namespace Scrobblint.Infrastructure.Persistence.Providers;

/// <summary>MySQL / MariaDB storage provider (via Pomelo).</summary>
public sealed class MySqlDataStorageProvider : IEfDataStorageProvider
{
    /// <summary>Assembly that holds the MySQL-specific EF migrations.</summary>
    public const string MigrationsAssemblyName = "Scrobblint.Migrations.MySql";

    // Used only at design time (dotnet ef), where no live database is available to detect against.
    public static readonly Version DefaultServerVersion = new(8, 4, 0);

    // null => auto-detect the server version from the live database on first use.
    private readonly Version? _serverVersion;

    public MySqlDataStorageProvider(Version? serverVersion = null) =>
        _serverVersion = serverVersion;

    public string Name => "MySQL";
    public bool SupportsMigrations => true;

    public void Configure(DbContextOptionsBuilder options, string connectionString)
    {
        // Connection pooling is left ON (MySqlConnector's default) for throughput: a stats-heavy
        // page runs a dozen queries and re-establishing a connection each time is wasteful. The
        // "Packet received out-of-order" corruption that once forced pooling off is gone — reads now
        // use isolated short-lived contexts (IDbContextFactory) with CancellationToken.None and there
        // is no retrying execution strategy, so a query is never torn down mid-result and abandoned
        // back into the pool.

        // Auto-detect the server version unless one was pinned via Database:ServerVersion. Detection
        // opens a single connection on first use (at start-up, after the DB is healthy).
        var version = _serverVersion is null
            ? ServerVersion.AutoDetect(connectionString)
            : new MySqlServerVersion(_serverVersion);

        options.UseMySql(connectionString, version, mysql =>
        {
            mysql.MigrationsAssembly(MigrationsAssemblyName);
            // NOTE: deliberately no EnableRetryOnFailure. The retrying execution strategy treats a
            // connection torn down by request cancellation as a transient fault and retries on the
            // same DbContext while the cancelled operation is still tearing down — producing "a
            // second operation was started on this context instance". A failed SSR render is
            // recoverable by reload, so retrying costs more than it buys for a self-hosted DB.
        });
    }
}
