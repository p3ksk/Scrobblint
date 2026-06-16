namespace Scrobblint.Infrastructure.Configuration;

/// <summary>
/// Bound from "Lastfm". Last.fm scrobbling requires a registered application's API key and shared
/// secret (https://www.last.fm/api/account/create). When these are blank, Last.fm relaying is disabled.
/// </summary>
public sealed class LastfmOptions
{
    public const string SectionName = "Lastfm";

    public string? ApiKey { get; set; }
    public string? ApiSecret { get; set; }
    public string ApiRoot { get; set; } = "https://ws.audioscrobbler.com/2.0/";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(ApiSecret);
}
