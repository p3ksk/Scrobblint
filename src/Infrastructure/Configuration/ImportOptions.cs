namespace Scrobblint.Infrastructure.Configuration;

/// <summary>
/// Bound from "Import". Tunes the history-import worker — chiefly how politely it paces Last.fm
/// requests (Last.fm allows ~5 req/s; the default leaves comfortable headroom).
/// </summary>
public sealed class ImportOptions
{
    public const string SectionName = "Import";

    /// <summary>Delay between page requests, in milliseconds.</summary>
    public int PageDelayMilliseconds { get; set; } = 300;
}
