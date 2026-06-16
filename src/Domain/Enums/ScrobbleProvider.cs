namespace Scrobblint.Domain.Enums;

/// <summary>
/// An external scrobbling service that listens can be relayed (forwarded) to.
/// </summary>
public enum ScrobbleProvider
{
    /// <summary>ListenBrainz (or a self-hosted compatible instance), authenticated with a user token.</summary>
    ListenBrainz = 0,

    /// <summary>Last.fm, authenticated with a per-user session key.</summary>
    Lastfm = 1
}
