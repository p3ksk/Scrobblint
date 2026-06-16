namespace Scrobblint.Application.Abstractions;

/// <summary>
/// Abstracts the system clock so time-dependent logic can be tested deterministically.
/// </summary>
public interface IClock
{
    /// <summary>The current UTC time as a <see cref="DateTime"/> (Kind = Utc).</summary>
    DateTime UtcNow { get; }
}
