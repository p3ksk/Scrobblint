using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Scrobblint.IntegrationTests;

/// <summary>
/// Boots the real API against a throwaway SQLite database file (migrations applied + admin seeded
/// on start-up). Each instance is isolated.
/// </summary>
public sealed class ScrobblintApiFactory : WebApplicationFactory<Program>
{
    public const string AdminUsername = "admin";
    public const string AdminPassword = "AdminPass!123";

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"scrobblint-it-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "SQLite",
                ["Database:ConnectionString"] = $"Data Source={_dbPath}",
                ["Database:ApplyMigrationsOnStartup"] = "true",
                ["Seed:Admin:Enabled"] = "true",
                ["Seed:Admin:Username"] = AdminUsername,
                ["Seed:Admin:Email"] = "admin@example.com",
                ["Seed:Admin:Password"] = AdminPassword,
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { /* best effort */ }
    }
}
