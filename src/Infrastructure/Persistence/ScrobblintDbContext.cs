using Microsoft.EntityFrameworkCore;
using Scrobblint.Domain.Entities;

namespace Scrobblint.Infrastructure.Persistence;

/// <summary>
/// The EF Core unit of work for Scrobblint. Provider selection (SQLite / MySQL / …) happens
/// in <c>AddInfrastructure</c>; this context itself is provider-agnostic.
/// </summary>
public class ScrobblintDbContext : DbContext
{
    public ScrobblintDbContext(DbContextOptions<ScrobblintDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Scrobble> Scrobbles => Set<Scrobble>();
    public DbSet<UserSettings> UserSettings => Set<UserSettings>();
    public DbSet<ExternalConnection> ExternalConnections => Set<ExternalConnection>();
    public DbSet<ScrobbleImport> ScrobbleImports => Set<ScrobbleImport>();
    public DbSet<TrackInfo> TrackInfos => Set<TrackInfo>();
    public DbSet<FailedRelay> FailedRelays => Set<FailedRelay>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ScrobblintDbContext).Assembly);
    }
}
