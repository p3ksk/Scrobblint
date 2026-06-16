namespace Scrobblint.Application.Abstractions.Relay;

/// <summary>A single listen to forward to an external service. Timestamp is Unix seconds.</summary>
public sealed record RelayTrack(string Artist, string Track, string? Album, long ListenedAtUnix);

/// <summary>Outcome of a relay attempt.</summary>
public sealed record RelayResult(bool Success, int Accepted, string? Error)
{
    public static RelayResult Ok(int accepted) => new(true, accepted, null);
    public static RelayResult Fail(string error) => new(false, 0, error);
}

/// <summary>Result of authenticating/validating a connection, used during setup.</summary>
public sealed record RelayAuthResult(bool Success, string? Credential, string? Username, string? Error)
{
    public static RelayAuthResult Ok(string credential, string? username) => new(true, credential, username, null);
    public static RelayAuthResult Fail(string error) => new(false, null, null, error);
}

/// <summary>One page of historical listens fetched from a source during an import.</summary>
public sealed record RelayHistoryPage(IReadOnlyList<RelayTrack> Tracks, int Page, int TotalPages, int Total);

/// <summary>Result of fetching a history page.</summary>
public sealed record RelayHistoryResult(bool Success, RelayHistoryPage? Page, string? Error)
{
    public static RelayHistoryResult Ok(RelayHistoryPage page) => new(true, page, null);
    public static RelayHistoryResult Fail(string error) => new(false, null, error);
}
