namespace Scrobblint.Domain.Entities;

/// <summary>
/// An account that owns scrobbles and authenticates against the API with a personal token.
/// </summary>
public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Unique, case-insensitive login name.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Unique e-mail address.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Hashed password produced by the password hasher. Never the plain text.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// Personal API token used for ListenBrainz-style <c>Authorization: Token …</c> access.
    /// Regenerated on demand; only ever stored hashed-free because it acts as a bearer secret.
    /// </summary>
    public string ApiToken { get; set; } = string.Empty;

    /// <summary>Creation time, stored in UTC (a sortable type across all providers).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Whether the user has administrative privileges.</summary>
    public bool IsAdmin { get; set; }

    /// <summary>
    /// When true the account is blocked: it cannot log in, submit scrobbles, or be viewed publicly.
    /// </summary>
    public bool IsDisabled { get; set; }

    // Navigation properties
    public UserSettings? Settings { get; set; }
    public ICollection<Scrobble> Scrobbles { get; set; } = new List<Scrobble>();
}
