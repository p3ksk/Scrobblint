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
        // Disable MySqlConnector's physical-connection pool. When a Blazor request is aborted
        // (enhanced navigation cancels the in-flight request the moment you click another link),
        // the running query can be torn down mid-result, leaving its connection half-read. A pooled
        // connection in that state poisons the NEXT request: leasing it runs a reset that reads the
        // stale packet and throws "Packet received out-of-order. Expected 1; got 2." Opening a fresh
        // connection per operation makes a cancelled request fail only itself — it can never corrupt
        // another. The extra connect cost is negligible for a co-located, low-traffic self-hosted DB.
        var builder = new MySqlConnector.MySqlConnectionStringBuilder(connectionString) { Pooling = false };

        // Auto-detect the server version unless one was pinned via Database:ServerVersion. Detection
        // opens a single connection on first use (at start-up, after the DB is healthy).
        var version = _serverVersion is null
            ? ServerVersion.AutoDetect(builder.ConnectionString)
            : new MySqlServerVersion(_serverVersion);

        options.UseMySql(builder.ConnectionString, version, mysql =>
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
