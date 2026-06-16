namespace Scrobblint.Domain.Enums;

/// <summary>
/// User interface colour theme preference.
/// </summary>
public enum Theme
{
    /// <summary>Follow the operating system / browser preference.</summary>
    System = 0,

    /// <summary>Force the light theme.</summary>
    Light = 1,

    /// <summary>Force the dark theme.</summary>
    Dark = 2
}
