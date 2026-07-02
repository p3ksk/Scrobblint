using Scrobblint.Application.Common;
using Scrobblint.Shared.Auth;
using Scrobblint.Shared.Common;
using Scrobblint.Shared.Users;

namespace Scrobblint.Application.Services;

/// <summary>
/// User profile, settings and administrative management.
/// </summary>
public interface IUserService
{
    Task<Result<UserProfileResponse>> GetProfileAsync(
        string username, ViewerContext viewer, CancellationToken cancellationToken = default);

    Task<Result<UserSettingsDto>> GetSettingsAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<Result<UserSettingsDto>> UpdateSettingsAsync(
        Guid userId, UserSettingsDto settings, CancellationToken cancellationToken = default);

    // ----- Administration -----

    Task<Result<PagedResponse<AdminUserListItem>>> GetUsersAsync(
        int page, int pageSize, string? search, CancellationToken cancellationToken = default);

    Task<Result<AdminUserDetail>> GetUserDetailAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<Result> SetDisabledAsync(Guid userId, bool disabled, CancellationToken cancellationToken = default);

    Task<Result> SetAdminAsync(Guid userId, bool isAdmin, CancellationToken cancellationToken = default);

    Task<Result<TokenResponse>> RegenerateUserTokenAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Permanently deletes a user and all their data (scrobbles, settings, connections).</summary>
    Task<Result> DeleteUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
