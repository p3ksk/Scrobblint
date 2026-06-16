using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Scrobblint.Infrastructure.Persistence;

namespace Scrobblint.Migrations.Sqlite;

/// <summary>
/// Design-time factory so <c>dotnet ef</c> can build the context for the SQLite migration set
/// without booting the whole application.
/// </summary>
public sealed class SqliteDesignTimeContextFactory : IDesignTimeDbContextFactory<ScrobblintDbContext>
{
    public ScrobblintDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ScrobblintDbContext>()
            .UseSqlite("Data Source=scrobblint-design.db",
                sqlite => sqlite.MigrationsAssembly(typeof(SqliteDesignTimeContextFactory).Assembly.GetName().Name))
            .Options;

        return new ScrobblintDbContext(options);
    }
}
