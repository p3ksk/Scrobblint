using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Scrobblint.Application.Abstractions;
using Scrobblint.Application.Abstractions.Relay;
using Scrobblint.Application.Services;
using Scrobblint.Domain.Entities;
using Scrobblint.Domain.Enums;
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
    public ScrobbleImportService Imports { get; }
    public FakeLastfmRelay Lastfm { get; } = new();

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

        var importRepo = new ScrobbleImportRepository(Db);
        var connectionRepo = new ExternalConnectionRepository(Db);

        Auth = new AuthService(userRepo, settingsRepo, hasher, tokens, unitOfWork, Clock, NullLogger<AuthService>.Instance);
        Scrobbles = new ScrobbleService(scrobbleRepo, userRepo, settingsRepo, connectionRepo, unitOfWork, RelayQueue, new IScrobbleRelay[] { Lastfm }, Clock, new MemoryCache(Options.Create(new MemoryCacheOptions())), NullLogger<ScrobbleService>.Instance);
        Statistics = new StatisticsService(scrobbleRepo, userRepo, settingsRepo);
        Users = new UserService(userRepo, settingsRepo, scrobbleRepo, tokens, unitOfWork, NullLogger<UserService>.Instance);
        Imports = new ScrobbleImportService(importRepo, connectionRepo, scrobbleRepo, Lastfm, new NoopImportQueue(), unitOfWork, Clock, NullLogger<ScrobbleImportService>.Instance);
    }

    /// <summary>Inserts a Last.fm connection so an import can be started.</summary>
    public async Task ConnectLastfmAsync(Guid userId, string account)
    {
        Db.ExternalConnections.Add(new ExternalConnection
        {
            UserId = userId,
            Provider = ScrobbleProvider.Lastfm,
            IsEnabled = true,
            Token = "sk",
            ExternalUsername = account,
            CreatedAt = Clock.UtcNow
        });
        await Db.SaveChangesAsync();
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

public sealed class NoopImportQueue : IScrobbleImportQueue
{
    public bool Enqueue(Guid importId) => true;

    public async IAsyncEnumerable<Guid> DequeueAllAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        yield break;
    }
}

/// <summary>Fake Last.fm relay returning canned history pages keyed by page number.</summary>
public sealed class FakeLastfmRelay : ILastfmRelay
{
    public Dictionary<int, RelayHistoryPage> Pages { get; } = new();

    public ScrobbleProvider Provider => ScrobbleProvider.Lastfm;
    public bool IsConfigured => true;

    public Task<RelayHistoryResult> GetRecentTracksAsync(string username, int page, int limit, long? toUnix, CancellationToken cancellationToken = default) =>
        Task.FromResult(Pages.TryGetValue(page, out var p)
            ? RelayHistoryResult.Ok(p)
            : RelayHistoryResult.Fail($"No page {page}"));

    public Task<RelayResult> SendAsync(ExternalConnection connection, IReadOnlyList<RelayTrack> tracks, CancellationToken cancellationToken = default) =>
        Task.FromResult(RelayResult.Ok(tracks.Count));
    public Task<RelayResult> SendNowPlayingAsync(ExternalConnection connection, string artist, string track, string? album, CancellationToken cancellationToken = default) =>
        Task.FromResult(RelayResult.Ok(1));
    public string BuildAuthorizeUrl(string callbackUrl) => callbackUrl;
    public Task<RelayAuthResult> CompleteAuthorizationAsync(string token, CancellationToken cancellationToken = default) =>
        Task.FromResult(RelayAuthResult.Ok("sk", "tester"));
}
