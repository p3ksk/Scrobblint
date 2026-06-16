using Scrobblint.Infrastructure.Configuration;

namespace Scrobblint.Infrastructure.Persistence.Providers;

/// <summary>
/// Resolves the configured <see cref="IEfDataStorageProvider"/> from the provider key.
/// Extend the switch to light up PostgreSQL, SQL Server or MongoDB in future.
/// </summary>
public static class DataStorageProviderFactory
{
    public static IEfDataStorageProvider Create(DatabaseOptions options)
    {
        var provider = (options.Provider ?? "SQLite").Trim();

        return provider.ToLowerInvariant() switch
        {
            "sqlite" => new SqliteDataStorageProvider(),
            "mysql" or "mariadb" => new MySqlDataStorageProvider(ParseVersion(options.ServerVersion)),

            // Reserved for future providers — implement IEfDataStorageProvider and add a case here.
            "postgres" or "postgresql" => throw NotYetSupported(provider),
            "sqlserver" or "mssql" => throw NotYetSupported(provider),
            "mongodb" or "mongo" => throw NotYetSupported(provider),

            _ => throw new NotSupportedException(
                $"Unknown database provider '{provider}'. Supported: SQLite, MySQL.")
        };
    }

    public static string ResolveConnectionString(DatabaseOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ConnectionString))
            return options.ConnectionString!;

        // Out-of-the-box default for SQLite so the app runs with zero configuration.
        if (string.Equals(options.Provider, "SQLite", StringComparison.OrdinalIgnoreCase))
            return "Data Source=scrobblint.db";

        throw new InvalidOperationException(
            $"A connection string is required for the '{options.Provider}' provider (set Database:ConnectionString).");
    }

    private static Version? ParseVersion(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        // Accept forms like "8.4.0" or "10.11.0-mariadb"; take the leading numeric portion.
        var numeric = new string(raw.TakeWhile(c => char.IsDigit(c) || c == '.').ToArray());
        return Version.TryParse(numeric, out var version) ? version : null;
    }

    private static NotSupportedException NotYetSupported(string provider) => new(
        $"The '{provider}' provider is planned but not yet implemented. " +
        "Implement IEfDataStorageProvider and register it in DataStorageProviderFactory.");
}
