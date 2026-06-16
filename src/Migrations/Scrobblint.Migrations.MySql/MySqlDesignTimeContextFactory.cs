using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Scrobblint.Infrastructure.Persistence;
using Scrobblint.Infrastructure.Persistence.Providers;

namespace Scrobblint.Migrations.MySql;

/// <summary>
/// Design-time factory so <c>dotnet ef</c> can build the context for the MySQL migration set.
/// A fixed server version is used so no live database is required to scaffold migrations.
/// </summary>
public sealed class MySqlDesignTimeContextFactory : IDesignTimeDbContextFactory<ScrobblintDbContext>
{
    public ScrobblintDbContext CreateDbContext(string[] args)
    {
        // Connection string is only needed for the syntax; it is never opened at design time.
        const string placeholderConnection = "Server=localhost;Port=3306;Database=scrobblint;User=root;Password=root;";

        var options = new DbContextOptionsBuilder<ScrobblintDbContext>()
            .UseMySql(placeholderConnection, new MySqlServerVersion(MySqlDataStorageProvider.DefaultServerVersion),
                mysql => mysql.MigrationsAssembly(typeof(MySqlDesignTimeContextFactory).Assembly.GetName().Name))
            .Options;

        return new ScrobblintDbContext(options);
    }
}
