using Scrobblint.Application.Common;
using Scrobblint.Shared.Auth;
using Scrobblint.Shared.Scrobbles;
using Xunit;

namespace Scrobblint.UnitTests;

public class ScrobbleServiceTests
{
    private static async Task<Guid> SeedUserAsync(TestHost host, string name = "alice")
    {
        var reg = await host.Auth.RegisterAsync(new RegisterRequest(name, $"{name}@example.com", "supersecret"));
        return reg.Value!.Id;
    }

    [Fact]
    public async Task Submit_stores_scrobble()
    {
        using var host = new TestHost();
        var userId = await SeedUserAsync(host);

        var result = await host.Scrobbles.SubmitAsync(userId, new ScrobbleRequest("Radiohead", "Idioteque", "Kid A"));

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.Value!.Accepted);
    }

    [Fact]
    public async Task Submit_missing_artist_fails_validation()
    {
        using var host = new TestHost();
        var userId = await SeedUserAsync(host);

        var result = await host.Scrobbles.SubmitAsync(userId, new ScrobbleRequest("", "Idioteque"));

        Assert.True(result.Failed);
        Assert.Equal(ResultError.Validation, result.Error);
    }

    [Fact]
    public async Task Submit_future_timestamp_rejected()
    {
        using var host = new TestHost();
        var userId = await SeedUserAsync(host);
        var future = new DateTimeOffset(host.Clock.UtcNow.AddDays(5)).ToUnixTimeSeconds();

        var result = await host.Scrobbles.SubmitAsync(userId, new ScrobbleRequest("A", "B", null, future));

        Assert.True(result.Failed);
        Assert.Equal(ResultError.Validation, result.Error);
    }

    [Fact]
    public async Task Submit_enqueues_scrobble_to_pipeline()
    {
        using var host = new TestHost();
        var userId = await SeedUserAsync(host);

        await host.Scrobbles.SubmitAsync(userId, new ScrobbleRequest("Radiohead", "Idioteque", "Kid A"));

        var scrobble = Assert.Single(host.PipelineQueue.Jobs);
        Assert.Equal(userId, scrobble.UserId);
        Assert.Equal("Radiohead", scrobble.Artist);
        Assert.Equal("Idioteque", scrobble.Track);
        Assert.Equal("Kid A", scrobble.Album);
    }

    [Fact]
    public async Task Submit_batch_accepts_all()
    {
        using var host = new TestHost();
        var userId = await SeedUserAsync(host);

        var batch = new ScrobbleBatchRequest(new[]
        {
            new ScrobbleRequest("A", "1"),
            new ScrobbleRequest("B", "2"),
            new ScrobbleRequest("C", "3"),
        });

        var result = await host.Scrobbles.SubmitBatchAsync(userId, batch);

        Assert.True(result.Succeeded);
        Assert.Equal(3, result.Value!.Accepted);
    }

    [Fact]
    public async Task Recent_is_paged_and_newest_first()
    {
        using var host = new TestHost();
        var userId = await SeedUserAsync(host);

        for (var i = 0; i < 5; i++)
            host.Clock.UtcNow = host.Clock.UtcNow.AddMinutes(1);
        // submit five scrobbles with increasing timestamps
        var baseTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        for (var i = 0; i < 5; i++)
            await host.Scrobbles.SubmitAsync(userId, new ScrobbleRequest("Artist", $"Track{i}", null, baseTime.AddMinutes(i).ToUnixTimeSeconds()));
        await host.DrainPipelineAsync();

        var owner = new ViewerContext(userId, false);
        var page1 = await host.Scrobbles.GetRecentAsync("alice", 1, 2, owner);

        Assert.True(page1.Succeeded);
        Assert.Equal(5, page1.Value!.TotalCount);
        Assert.Equal(2, page1.Value.Items.Count);
        Assert.Equal("Track4", page1.Value.Items[0].Track); // newest first
    }

    [Fact]
    public async Task Recent_on_private_profile_is_forbidden_for_anonymous()
    {
        using var host = new TestHost();
        var userId = await SeedUserAsync(host);
        await host.Users.UpdateSettingsAsync(userId, new Scrobblint.Shared.Users.UserSettingsDto(
            Scrobblint.Domain.Enums.ProfileVisibility.Private, Scrobblint.Domain.Enums.Theme.System));

        var anon = await host.Scrobbles.GetRecentAsync("alice", 1, 10, ViewerContext.Anonymous);
        Assert.True(anon.Failed);
        Assert.Equal(ResultError.Forbidden, anon.Error);

        var owner = await host.Scrobbles.GetRecentAsync("alice", 1, 10, new ViewerContext(userId, false));
        Assert.True(owner.Succeeded);
    }
}
