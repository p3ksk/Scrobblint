namespace Scrobblint.Domain.Enums;

/// <summary>Lifecycle of a scrobble history import.</summary>
public enum ImportStatus
{
    /// <summary>Created, waiting for the background worker to pick it up.</summary>
    Pending = 0,

    /// <summary>Actively fetching and importing pages.</summary>
    Running = 1,

    /// <summary>All pages imported.</summary>
    Completed = 2,

    /// <summary>Stopped by an error (see the message).</summary>
    Failed = 3,

    /// <summary>Cancelled by the user.</summary>
    Cancelled = 4
}
