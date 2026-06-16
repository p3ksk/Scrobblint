using Microsoft.EntityFrameworkCore;

namespace Scrobblint.Infrastructure.Persistence.Providers;

/// <summary>SQLite storage provider — the zero-dependency default, ideal for self-hosting.</summary>
public sealed class SqliteDataStorageProvider : IEfDataStorageProvider
{
    /// <summary>Assembly that holds the SQLite-specific EF migrations.</summary>
    public const string MigrationsAssemblyName = "Scrobblint.Migrations.Sqlite";

    public string Name => "SQLite";
    public bool SupportsMigrations => true;

    public void Configure(DbContextOptionsBuilder options, string connectionString)
    {
        options.UseSqlite(connectionString, sqlite =>
            sqlite.MigrationsAssembly(MigrationsAssemblyName));
    }
}
