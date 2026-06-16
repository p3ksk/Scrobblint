using Scrobblint.Domain.Enums;

namespace Scrobblint.Domain.Entities;

/// <summary>
/// A bulk import of a user's listening history from an external service. Tracks progress so the job
/// can survive restarts and resume where it left off, and so the UI can show how far along it is.
/// </summary>
public class ScrobbleImport
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public ScrobbleProvider Provider { get; set; }

    public ImportStatus Status { get; set; } = ImportStatus.Pending;

    /// <summary>The external account the history is pulled from (e.g. the Last.fm username).</summary>
    public string SourceAccount { get; set; } = string.Empty;

    /// <summary>
    /// Upper time bound (Unix seconds), snapshotted at start, so paging is over a stable set even while
    /// new scrobbles arrive during the (long) import.
    /// </summary>
    public long ToTimestamp { get; set; }

    /// <summary>Next page to fetch (1-based). Persisted after every page so the job is resumable.</summary>
    public int NextPage { get; set; } = 1;

    /// <summary>Total pages reported by the source (known after the first page; 0 until then).</summary>
    public int TotalPages { get; set; }

    /// <summary>Total scrobbles reported as available by the source.</summary>
    public int TotalAvailable { get; set; }

    /// <summary>Scrobbles actually inserted so far.</summary>
    public int ImportedCount { get; set; }

    /// <summary>Scrobbles skipped because they already existed.</summary>
    public int DuplicateCount { get; set; }

    public string? Error { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public bool IsActive => Status is ImportStatus.Pending or ImportStatus.Running;

    // Navigation property
    public User? User { get; set; }
}
