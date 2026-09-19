using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Scrobblint.Application.Abstractions;
using Scrobblint.Application.Common;
using Scrobblint.Application.Services;
using Scrobblint.Domain.Entities;
using Scrobblint.Domain.Enums;
using Scrobblint.Infrastructure.Configuration;
using Scrobblint.Infrastructure.Statistics;
using Scrobblint.Shared.Auth;
using Scrobblint.Shared.Scrobbles;
using Scrobblint.Shared.Users;
using Xunit;

namespace Scrobblint.UnitTests;

public class StatisticsSnapshotServiceTests
{
    private static async Task<Guid> SeedAsync(TestHost host, string name = "alice")
    {
        var reg = await host.Auth.RegisterAsync(new RegisterRequest(name, $"{name}@example.com", "supersecret"));
        return reg.Value!.Id;
    }

    private static async Task SubmitAsync(TestHost host, Guid userId, params ScrobbleRequest[] scrobbles)
    {
        await host.Scrobbles.SubmitBatchAsync(userId, new ScrobbleBatchRequest(scrobbles));
        await host.DrainPipelineAsync();
    }

    [Fact]
    public async Task All_time_read_persists_then_serves_the_snapshot()
    {
        using var host = new TestHost();
        var userId = await SeedAsync(host);
        await SubmitAsync(host, userId, new ScrobbleRequest("Radiohead", "Nude", "In Rainbows"));

        var first = await host.StatisticsSnapshots.GetStatsAsync("alice", new ViewerContext(userId, false));
        Assert.True(first.Succeeded);
        Assert.Equal(1, first.Value!.TotalScrobbles);

        var stored = await host.StatisticsRepo.GetUserAsync(userId);
        Assert.NotNull(stored);

        // A later listen does not change the served snapshot until the worker or an invalidation runs.
        await SubmitAsync(host, userId, new ScrobbleRequest("Radiohead", "Nude", "In Rainbows"));

        var second = await host.StatisticsSnapshots.GetStatsAsync("alice", new ViewerContext(userId, false));
        Assert.Equal(1, second.Value!.TotalScrobbles);
    }

    [Fact]
    public async Task Date_ranged_read_bypasses_the_snapshot()
    {
        using var host = new TestHost();
        var userId = await SeedAsync(host);
        await SubmitAsync(host, userId, new ScrobbleRequest("Radiohead", "Nude", "In Rainbows"));

        var ranged = await host.StatisticsSnapshots.GetStatsAsync(
            "alice", new ViewerContext(userId, false), host.Clock.UtcNow.AddYears(-1), host.Clock.UtcNow.AddDays(1));

        Assert.True(ranged.Succeeded);
        Assert.Null(await host.StatisticsRepo.GetUserAsync(userId));
    }

    [Fact]
    public async Task Private_profile_is_forbidden_without_serving_the_snapshot()
    {
        using var host = new TestHost();
        var aliceId = await SeedAsync(host, "alice");
        var bobId = await SeedAsync(host, "bob");
        await host.Users.UpdateSettingsAsync(aliceId, new UserSettingsDto(ProfileVisibility.Private, Theme.System));

        var asBob = await host.StatisticsSnapshots.GetStatsAsync("alice", new ViewerContext(bobId, false));

        Assert.True(asBob.Failed);
        Assert.Equal(ResultError.Forbidden, asBob.Error);
        Assert.Null(await host.StatisticsRepo.GetUserAsync(aliceId));
    }

    [Fact]
    public async Task Changing_settings_invalidates_the_stored_snapshot()
    {
        using var host = new TestHost();
        var userId = await SeedAsync(host);
        await SubmitAsync(host, userId, new ScrobbleRequest("Radiohead", "Nude", "In Rainbows"));

        await host.StatisticsSnapshots.GetStatsAsync("alice", new ViewerContext(userId, false));
        Assert.NotNull(await host.StatisticsRepo.GetUserAsync(userId));

        await host.Users.UpdateSettingsAsync(userId,
            new UserSettingsDto(ProfileVisibility.Public, Theme.System, Timezone: "Asia/Kathmandu"));

        Assert.Null(await host.StatisticsRepo.GetUserAsync(userId));
    }

    [Fact]
    public async Task Global_read_persists_the_snapshot()
    {
        using var host = new TestHost();
        await SeedAsync(host);

        var first = await host.StatisticsSnapshots.GetGlobalStatsAsync();
        Assert.True(first.Succeeded);

        var stored = await host.StatisticsRepo.GetGlobalAsync();
        Assert.NotNull(stored);
        Assert.Equal(1, stored!.Id);
    }

    [Fact]
    public async Task Malformed_snapshot_is_treated_as_a_miss_and_recomputed()
    {
        using var host = new TestHost();
        var userId = await SeedAsync(host);
        await SubmitAsync(host, userId, new ScrobbleRequest("Radiohead", "Nude", "In Rainbows"));

        // A payload written by an older/newer contract (missing required fields) must not be served.
        await host.StatisticsRepo.UpsertUserAsync(userId, "{\"totalScrobbles\":999}", host.Clock.UtcNow);

        var result = await host.StatisticsSnapshots.GetStatsAsync("alice", new ViewerContext(userId, false));

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.Value!.TotalScrobbles); // recomputed, not the bogus 999

        var repaired = await host.StatisticsRepo.GetUserAsync(userId);
        Assert.Contains("monthlyChart", repaired!.PayloadJson); // rewritten with a complete payload
    }

    [Fact]
    public async Task Worker_refreshes_active_users_only()
    {
        using var host = new TestHost();
        var activeId = await SeedAsync(host, "alice");
        var inactiveId = await SeedAsync(host, "bob");
        await SubmitAsync(host, activeId, new ScrobbleRequest("Radiohead", "Nude", "In Rainbows"));

        // Bob's listen was received long before the active window.
        host.Db.Scrobbles.Add(new Scrobble
        {
            UserId = inactiveId,
            Artist = "Aphex Twin",
            Track = "Xtal",
            Timestamp = host.Clock.UtcNow.AddDays(-90),
            CreatedAt = host.Clock.UtcNow.AddDays(-90)
        });
        await host.Db.SaveChangesAsync();

        var provider = new ServiceCollection()
            .AddSingleton<IStatisticsComputer>(host.Statistics)
            .AddSingleton(host.StatisticsRepo)
            .AddSingleton(host.ScrobbleRepo)
            .AddSingleton<IClock>(host.Clock)
            .BuildServiceProvider();

        var worker = new StatisticsPrecomputeWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new StatisticsOptions { ActiveWindowDays = 30 }),
            NullLogger<StatisticsPrecomputeWorker>.Instance);

        await worker.RunOnceAsync(CancellationToken.None);

        Assert.NotNull(await host.StatisticsRepo.GetUserAsync(activeId));
        Assert.Null(await host.StatisticsRepo.GetUserAsync(inactiveId));
        Assert.NotNull(await host.StatisticsRepo.GetGlobalAsync());
    }
}
