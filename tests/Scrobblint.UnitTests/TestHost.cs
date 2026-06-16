using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Scrobblint.Application.Abstractions;
using Scrobblint.Application.Abstractions.Relay;
using Scrobblint.Application.Services;
using Scrobblint.Infrastructure.Persistence;
using Scrobblint.Infrastructure.Persistence.Repositories;
using Scrobblint.Infrastructure.Security;

namespace Scrobblint.UnitTests;

/// <summary>
/// Spins up a real (in-memory SQLite) database with the real repositories and services, so service
/// behaviour is exercised against actual EF query translation — not mocks.
/// </summary>
public sealed class TestHost : IDisposable
{
    private readonly SqliteConnection _connection;
    public ScrobblintDbContext Db { get; }
    public FakeClock Clock { get; } = new();
    public RecordingRelayQueue RelayQueue { get; } = new();

    public AuthService Auth { get; }
    public ScrobbleService Scrobbles { get; }
    public StatisticsService Statistics { get; }
    public UserService Users { get; }

    public TestHost()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ScrobblintDbContext>()
            .UseSqlite(_connection)
            .Options;
        Db = new ScrobblintDbContext(options);
        Db.Database.EnsureCreated();

        var userRepo = new UserRepository(Db);
        var scrobbleRepo = new ScrobbleRepository(Db);
        var settingsRepo = new UserSettingsRepository(Db);
        var unitOfWork = new UnitOfWork(Db);
        var hasher = new Pbkdf2PasswordHasher();
        var tokens = new TokenGenerator();

        Auth = new AuthService(userRepo, settingsRepo, hasher, tokens, unitOfWork, Clock, NullLogger<AuthService>.Instance);
        Scrobbles = new ScrobbleService(scrobbleRepo, userRepo, settingsRepo, unitOfWork, RelayQueue, Clock, NullLogger<ScrobbleService>.Instance);
        Statistics = new StatisticsService(scrobbleRepo, userRepo, settingsRepo);
        Users = new UserService(userRepo, settingsRepo, scrobbleRepo, tokens, unitOfWork, NullLogger<UserService>.Instance);
    }

    public void Dispose()
    {
        Db.Dispose();
        _connection.Dispose();
    }
}

public sealed class FakeClock : IClock
{
    public DateTime UtcNow { get; set; } = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
}

/// <summary>Captures relay jobs instead of forwarding, so tests can assert what would be relayed.</summary>
public sealed class RecordingRelayQueue : IScrobbleRelayQueue
{
    public List<ScrobbleRelayJob> Jobs { get; } = new();

    public bool Enqueue(ScrobbleRelayJob job)
    {
        Jobs.Add(job);
        return true;
    }

    public async IAsyncEnumerable<ScrobbleRelayJob> DequeueAllAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var job in Jobs)
            yield return job;
        await Task.CompletedTask;
    }
}
