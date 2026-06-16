namespace Scrobblint.Domain.Enums;

/// <summary>
/// Controls who may view a user's profile and listening history.
/// </summary>
public enum ProfileVisibility
{
    /// <summary>Anyone, including anonymous visitors, may view the profile.</summary>
    Public = 0,

    /// <summary>Only the owner (and administrators) may view the profile.</summary>
    Private = 1
}
