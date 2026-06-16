using Scrobblint.Application.Common;
using Scrobblint.Domain.Enums;
using Scrobblint.Shared.Auth;
using Scrobblint.Shared.Scrobbles;
using Scrobblint.Shared.Users;
using Xunit;

namespace Scrobblint.UnitTests;

public class StatisticsAndUserServiceTests
{
    private static async Task<Guid> SeedAsync(TestHost host, string name = "alice")
    {
        var reg = await host.Auth.RegisterAsync(new RegisterRequest(name, $"{name}@example.com", "supersecret"));
        return reg.Value!.Id;
    }

    [Fact]
    public async Task Stats_compute_totals_and_top_lists()
    {
        using var host = new TestHost();
        var userId = await SeedAsync(host);

        await host.Scrobbles.SubmitBatchAsync(userId, new ScrobbleBatchRequest(new[]
        {
            new ScrobbleRequest("Radiohead", "Idioteque", "Kid A"),
            new ScrobbleRequest("Radiohead", "Idioteque", "Kid A"),
            new ScrobbleRequest("Radiohead", "Nude", "In Rainbows"),
            new ScrobbleRequest("Aphex Twin", "Xtal", "SAW"),
        }));

        var stats = await host.Statistics.GetStatsAsync("alice", new ViewerContext(userId, false));

        Assert.True(stats.Succeeded);
        Assert.Equal(4, stats.Value!.TotalScrobbles);
        Assert.Equal(2, stats.Value.UniqueArtists);
        Assert.Equal(3, stats.Value.UniqueTracks);
        Assert.Equal("Radiohead", stats.Value.TopArtists[0].Artist);
        Assert.Equal(3, stats.Value.TopArtists[0].Count);
        Assert.Equal("Idioteque", stats.Value.TopTracks[0].Track);
        Assert.Equal(2, stats.Value.TopTracks[0].Count);
    }

    [Fact]
    public async Task UpdateSettings_persists_visibility_and_theme()
    {
        using var host = new TestHost();
        var userId = await SeedAsync(host);

        var updated = await host.Users.UpdateSettingsAsync(userId, new UserSettingsDto(ProfileVisibility.Private, Theme.Dark));
        Assert.True(updated.Succeeded);

        var read = await host.Users.GetSettingsAsync(userId);
        Assert.Equal(ProfileVisibility.Private, read.Value!.ProfileVisibility);
        Assert.Equal(Theme.Dark, read.Value.Theme);
    }

    [Fact]
    public async Task Admin_can_disable_user_and_list_reflects_it()
    {
        using var host = new TestHost();
        var userId = await SeedAsync(host);

        var disabled = await host.Users.SetDisabledAsync(userId, true);
        Assert.True(disabled.Succeeded);

        // A disabled user cannot authenticate by token.
        var reg = await host.Auth.LoginAsync(new LoginRequest("alice", "supersecret"));
        Assert.True(reg.Failed);
        Assert.Equal(ResultError.Forbidden, reg.Error);

        var list = await host.Users.GetUsersAsync(1, 25, null);
        Assert.True(list.Succeeded);
        Assert.Contains(list.Value!.Items, u => u.Username == "alice" && u.IsDisabled);
    }

    [Fact]
    public async Task Admin_list_search_filters_by_username()
    {
        using var host = new TestHost();
        await SeedAsync(host, "alice");
        await SeedAsync(host, "bob");

        var list = await host.Users.GetUsersAsync(1, 25, "bob");

        Assert.True(list.Succeeded);
        Assert.Single(list.Value!.Items);
        Assert.Equal("bob", list.Value.Items[0].Username);
    }

    [Fact]
    public async Task Profile_hidden_when_private_and_viewer_is_other_user()
    {
        using var host = new TestHost();
        var aliceId = await SeedAsync(host, "alice");
        var bobId = await SeedAsync(host, "bob");
        await host.Users.UpdateSettingsAsync(aliceId, new UserSettingsDto(ProfileVisibility.Private, Theme.System));

        var asBob = await host.Users.GetProfileAsync("alice", new ViewerContext(bobId, false));
        Assert.True(asBob.Failed);
        Assert.Equal(ResultError.Forbidden, asBob.Error);

        var asAdmin = await host.Users.GetProfileAsync("alice", new ViewerContext(bobId, true));
        Assert.True(asAdmin.Succeeded);
    }
}
