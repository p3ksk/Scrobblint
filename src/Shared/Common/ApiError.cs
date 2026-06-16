namespace Scrobblint.Shared.Common;

/// <summary>
/// Uniform error envelope returned by the API for non-success responses.
/// </summary>
public sealed record ApiError(string Message, IReadOnlyDictionary<string, string[]>? Errors = null);
