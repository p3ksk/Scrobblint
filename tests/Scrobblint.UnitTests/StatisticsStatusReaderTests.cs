using Microsoft.Extensions.Options;
using Scrobblint.Application.Common;
using Scrobblint.Domain.Entities;
using Scrobblint.Infrastructure.Configuration;
using Scrobblint.Infrastructure.Statistics;
using Scrobblint.Shared.Auth;
using Xunit;

namespace Scrobblint.UnitTests;

public class StatisticsStatusReaderTests
{
    private const string Payload = "{\"totalScrobbles\":1}";

    private static async Task<Guid> SeedAsync(TestHost host, string name)
    {
        var reg = await host.Auth.RegisterAsync(new RegisterRequest(name, $"{name}@example.com", "supersecret"));
        return reg.Value!.Id;
    }

    /// <summary>Adds a scrobble whose server-receive time decides whether the user counts as active.</summary>
    private static async Task AddScrobbleAsync(TestHost host, Guid userId, DateTime createdAt)
    {
        host.Db.Scrobbles.Add(new Scrobble
        {
            UserId = userId,
            Artist = "Radiohead",
            Track = "Nude",
            Timestamp = createdAt,
            CreatedAt = createdAt
        });
        await host.Db.SaveChangesAsync();
    }

    private static StatisticsStatusReader CreateReader(TestHost host, StatisticsOptions? options = null) =>
        new(host.Factory, host.ScrobbleRepo, Options.Create(options ?? new StatisticsOptions()), host.Clock);

    [Fact]
    public async Task Status_reports_counts_timestamps_and_config()
    {
        using var host = new TestHost();
        var userId = await SeedAsync(host, "alice");
        await AddScrobbleAsync(host, userId, host.Clock.UtcNow);
        await host.StatisticsRepo.UpsertUserAsync(userId, Payload, host.Clock.UtcNow);
        await host.StatisticsRepo.UpsertGlobalAsync(Payload, host.Clock.UtcNow);

        var status = await CreateReader(host).GetStatusAsync();

        Assert.Equal(Mappers.ToUnix(host.Clock.UtcNow), status.GlobalComputedAt);
        Assert.Equal(1, status.UserSnapshotCount);
        Assert.Equal(1, status.TotalUsers);
        Assert.Equal(Mappers.ToUnix(host.Clock.UtcNow), status.OldestUserComputedAt);
        Assert.Equal(Mappers.ToUnix(host.Clock.UtcNow), status.NewestUserComputedAt);
        Assert.Equal(1, status.ActiveUserCount);
        Assert.Equal(0, status.StaleActiveUserCount);
        Assert.Equal(60, status.IntervalMinutes);
        Assert.Equal(30, status.ActiveWindowDays);
        Assert.True(status.RunOnStartup);
    }

    [Fact]
    public async Task Status_reports_never_computed_when_no_snapshots_exist()
    {
        using var host = new TestHost();
        await SeedAsync(host, "alice");

        var status = await CreateReader(host).GetStatusAsync();

        Assert.Null(status.GlobalComputedAt);
        Assert.Null(status.OldestUserComputedAt);
        Assert.Null(status.NewestUserComputedAt);
        Assert.Equal(0, status.UserSnapshotCount);
        Assert.Equal(1, status.TotalUsers);
    }

    [Fact]
    public async Task Stale_count_only_includes_active_users()
    {
        using var host = new TestHost();
        var activeId = await SeedAsync(host, "alice");
        var inactiveId = await SeedAsync(host, "bob");

        await AddScrobbleAsync(host, activeId, host.Clock.UtcNow);
        await AddScrobbleAsync(host, inactiveId, host.Clock.UtcNow.AddDays(-90));

        // Both snapshots are older than the 60-minute interval.
        var staleAt = host.Clock.UtcNow.AddMinutes(-120);
        await host.StatisticsRepo.UpsertUserAsync(activeId, Payload, staleAt);
        await host.StatisticsRepo.UpsertUserAsync(inactiveId, Payload, staleAt);

        var status = await CreateReader(host).GetStatusAsync();

        Assert.Equal(2, status.UserSnapshotCount);
        Assert.Equal(1, status.ActiveUserCount);       // bob is outside the 30-day window
        Assert.Equal(1, status.StaleActiveUserCount);  // bob's frozen snapshot is not counted
    }

    [Fact]
    public async Task Snapshot_list_is_paged_oldest_first_with_usernames_and_sizes()
    {
        using var host = new TestHost();
        var aliceId = await SeedAsync(host, "alice");
        var bobId = await SeedAsync(host, "bob");
        await host.StatisticsRepo.UpsertUserAsync(aliceId, Payload, host.Clock.UtcNow.AddMinutes(-120));
        await host.StatisticsRepo.UpsertUserAsync(bobId, Payload, host.Clock.UtcNow.AddMinutes(-5));

        var reader = CreateReader(host);

        var first = await reader.GetUserSnapshotsAsync(1, 1);
        Assert.Equal(2, first.TotalCount);
        Assert.Equal(2, first.TotalPages);
        Assert.True(first.HasNext);
        var alice = Assert.Single(first.Items);
        Assert.Equal("alice", alice.Username);
        Assert.Equal(Payload.Length, alice.PayloadChars);
        Assert.True(alice.IsStale); // older than the 60-minute interval

        var second = await reader.GetUserSnapshotsAsync(2, 1);
        var bob = Assert.Single(second.Items);
        Assert.Equal("bob", bob.Username);
        Assert.False(bob.IsStale);
        Assert.False(second.HasNext);
    }
}
