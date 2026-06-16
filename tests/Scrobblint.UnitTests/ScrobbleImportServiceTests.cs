using Microsoft.EntityFrameworkCore;
using Scrobblint.Application.Abstractions.Relay;
using Scrobblint.Shared.Auth;
using Xunit;

namespace Scrobblint.UnitTests;

public class ScrobbleImportServiceTests
{
    private static RelayTrack T(string artist, string track, long uts) => new(artist, track, null, uts);

    private static async Task<Guid> SeedConnectedUserAsync(TestHost host)
    {
        var reg = await host.Auth.RegisterAsync(new RegisterRequest("alice", "alice@example.com", "supersecret"));
        await host.ConnectLastfmAsync(reg.Value!.Id, "alice_fm");
        return reg.Value.Id;
    }

    private static async Task DrainAsync(TestHost host, Guid importId)
    {
        // The worker normally loops this; in tests we drive it directly.
        while (await host.Imports.ProcessNextChunkAsync(importId)) { }
    }

    [Fact]
    public async Task Import_pages_through_history_and_deduplicates()
    {
        using var host = new TestHost();
        var userId = await SeedConnectedUserAsync(host);

        // 3 pages, 6 listens total, with one cross-page duplicate (Four Tet on pages 2 and 3).
        host.Lastfm.Pages[1] = new RelayHistoryPage(new[] { T("Radiohead", "Idioteque", 1700001000), T("Aphex Twin", "Xtal", 1700000990) }, 1, 3, 6);
        host.Lastfm.Pages[2] = new RelayHistoryPage(new[] { T("Burial", "Archangel", 1700000980), T("Four Tet", "Angel Echoes", 1700000970) }, 2, 3, 6);
        host.Lastfm.Pages[3] = new RelayHistoryPage(new[] { T("Four Tet", "Angel Echoes", 1700000970), T("Boards of Canada", "Roygbiv", 1700000960) }, 3, 3, 6);

        var start = await host.Imports.StartLastfmImportAsync(userId);
        Assert.True(start.Succeeded);

        await DrainAsync(host, await GetImportIdAsync(host, userId));

        var status = await host.Imports.GetStatusAsync(userId);
        Assert.Equal("Completed", status!.Status);
        Assert.Equal(5, status.ImportedCount);   // 6 fetched − 1 duplicate
        Assert.Equal(1, status.DuplicateCount);

        var recent = await host.Scrobbles.GetRecentAsync("alice", 1, 50, new Application.Common.ViewerContext(userId, false));
        Assert.Equal(5, recent.Value!.TotalCount);
    }

    [Fact]
    public async Task Re_running_import_does_not_duplicate_existing_scrobbles()
    {
        using var host = new TestHost();
        var userId = await SeedConnectedUserAsync(host);
        host.Lastfm.Pages[1] = new RelayHistoryPage(new[] { T("A", "1", 1700000000), T("B", "2", 1700000001) }, 1, 1, 2);

        // First import brings in the two listens.
        await host.Imports.StartLastfmImportAsync(userId);
        await DrainAsync(host, await GetImportIdAsync(host, userId));

        // Second import over the same data: everything is a duplicate.
        host.Clock.UtcNow = host.Clock.UtcNow.AddMinutes(1); // so the re-run is the newest import
        await host.Imports.StartLastfmImportAsync(userId);
        await DrainAsync(host, await GetImportIdAsync(host, userId));

        var status = await host.Imports.GetStatusAsync(userId);
        Assert.Equal("Completed", status!.Status);
        Assert.Equal(0, status.ImportedCount);
        Assert.Equal(2, status.DuplicateCount);

        var recent = await host.Scrobbles.GetRecentAsync("alice", 1, 50, new Application.Common.ViewerContext(userId, false));
        Assert.Equal(2, recent.Value!.TotalCount); // still just the original two
    }

    [Fact]
    public async Task Starting_without_a_connection_fails()
    {
        using var host = new TestHost();
        var reg = await host.Auth.RegisterAsync(new RegisterRequest("nolink", "n@e.com", "supersecret"));

        var result = await host.Imports.StartLastfmImportAsync(reg.Value!.Id);

        Assert.True(result.Failed);
    }

    private static async Task<Guid> GetImportIdAsync(TestHost host, Guid userId)
    {
        // Read the active/latest import id for test driving. AsNoTracking so this probe doesn't leave
        // the import tracked on the write context, which would clash when the worker re-attaches it.
        var import = await Task.FromResult(host.Db.ScrobbleImports.AsNoTracking()
            .Where(i => i.UserId == userId)
            .OrderByDescending(i => i.CreatedAt)
            .First());
        return import.Id;
    }
}
