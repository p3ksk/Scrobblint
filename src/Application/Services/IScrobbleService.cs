using Scrobblint.Application.Common;
using Scrobblint.Shared.Common;
using Scrobblint.Shared.Scrobbles;

namespace Scrobblint.Application.Services;

/// <summary>
/// Submission and retrieval of scrobbles.
/// </summary>
public interface IScrobbleService
{
    Task<Result<ScrobbleSubmitResponse>> SubmitAsync(
        Guid userId, ScrobbleRequest request, CancellationToken cancellationToken = default);

    Task<Result<ScrobbleSubmitResponse>> SubmitBatchAsync(
        Guid userId, ScrobbleBatchRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Recent scrobbles for <paramref name="username"/>, respecting profile visibility for the
    /// supplied <paramref name="viewer"/>.
    /// </summary>
    Task<Result<PagedResponse<ScrobbleResponse>>> GetRecentAsync(
        string username, int page, int pageSize, ViewerContext viewer, CancellationToken cancellationToken = default);
}
