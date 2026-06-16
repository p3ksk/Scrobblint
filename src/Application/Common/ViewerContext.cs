namespace Scrobblint.Application.Common;

/// <summary>
/// Identifies who is requesting data, so services can enforce profile visibility.
/// Use <see cref="Anonymous"/> for unauthenticated callers.
/// </summary>
public readonly record struct ViewerContext(Guid? UserId, bool IsAdmin)
{
    public static readonly ViewerContext Anonymous = new(null, false);

    public bool IsOwnerOf(Guid ownerId) => UserId is { } id && id == ownerId;

    /// <summary>True when this viewer may see private data belonging to <paramref name="ownerId"/>.</summary>
    public bool CanSeePrivate(Guid ownerId) => IsAdmin || IsOwnerOf(ownerId);
}
