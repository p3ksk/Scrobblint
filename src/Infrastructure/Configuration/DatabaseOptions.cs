namespace Scrobblint.Infrastructure.Configuration;

/// <summary>
/// Bound from the "Database" configuration section. Selects the storage engine and connection.
/// </summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>"SQLite" (default) or "MySQL".</summary>
    public string Provider { get; set; } = "SQLite";

    /// <summary>
    /// The connection string. When omitted, a sensible SQLite default ("Data Source=scrobblint.db")
    /// is used so the app runs out of the box.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>Optional MySQL/MariaDB server version override, e.g. "8.4.0" or "10.11.0-mariadb".</summary>
    public string? ServerVersion { get; set; }

    /// <summary>Apply EF migrations automatically on start-up. Default true.</summary>
    public bool ApplyMigrationsOnStartup { get; set; } = true;
}
