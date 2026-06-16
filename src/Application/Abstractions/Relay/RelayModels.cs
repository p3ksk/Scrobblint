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
