using Microsoft.Extensions.Logging;
using Scrobblint.Application.Abstractions.Persistence;
using Scrobblint.Application.Abstractions.Security;
using Scrobblint.Application.Common;
using Scrobblint.Domain.Entities;
using Scrobblint.Domain.Enums;
using Scrobblint.Shared.Auth;
using Scrobblint.Shared.Common;
using Scrobblint.Shared.Users;

namespace Scrobblint.Application.Services;

public sealed class UserService : IUserService
{
    private readonly IUserRepository _users;
    private readonly IUserSettingsRepository _settings;
    private readonly IScrobbleRepository _scrobbles;
    private readonly ITokenGenerator _tokenGenerator;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UserService> _logger;

    public UserService(
        IUserRepository users,
        IUserSettingsRepository settings,
        IScrobbleRepository scrobbles,
        ITokenGenerator tokenGenerator,
        IUnitOfWork unitOfWork,
        ILogger<UserService> logger)
    {
        _users = users;
        _settings = settings;
        _scrobbles = scrobbles;
        _tokenGenerator = tokenGenerator;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<UserProfileResponse>> GetProfileAsync(string username, ViewerContext viewer, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByUsernameAsync(username, cancellationToken);
        if (user is null || user.IsDisabled)
            return Result<UserProfileResponse>.NotFound("User not found.");

        var settings = await _settings.GetByUserIdAsync(user.Id, cancellationToken);
        var visibility = settings?.ProfileVisibility ?? ProfileVisibility.Public;
        if (visibility == ProfileVisibility.Private && !viewer.CanSeePrivate(user.Id))
            return Result<UserProfileResponse>.Forbidden("This profile is private.");

        var total = await _scrobbles.CountAsync(user.Id, cancellationToken: cancellationToken);
        var latest = await _scrobbles.GetLatestAsync(user.Id, cancellationToken);

        return Result<UserProfileResponse>.Ok(new UserProfileResponse(
            user.Username,
            Mappers.ToUnix(user.CreatedAt),
            user.IsAdmin,
            total,
            latest?.ToLite()));
    }

    public async Task<Result<UserSettingsDto>> GetSettingsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var settings = await _settings.GetByUserIdAsync(userId, cancellationToken);
        if (settings is null)
            return Result<UserSettingsDto>.NotFound("Settings not found.");
        return Result<UserSettingsDto>.Ok(settings.ToDto());
    }

    public async Task<Result<UserSettingsDto>> UpdateSettingsAsync(Guid userId, UserSettingsDto dto, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(dto.ProfileVisibility))
            return Result<UserSettingsDto>.Invalid(new Dictionary<string, string[]>
            {
                [nameof(dto.ProfileVisibility)] = new[] { "Unknown profile visibility value." }
            });
        if (!Enum.IsDefined(dto.Theme))
            return Result<UserSettingsDto>.Invalid(new Dictionary<string, string[]>
            {
                [nameof(dto.Theme)] = new[] { "Unknown theme value." }
            });

        var settings = await _settings.GetByUserIdAsync(userId, cancellationToken);
        if (settings is null)
        {
            settings = new UserSettings { UserId = userId };
            await _settings.AddAsync(settings, cancellationToken);
        }

        settings.ProfileVisibility = dto.ProfileVisibility;
        settings.Theme = dto.Theme;
        settings.TrackIgnoreRegex = string.IsNullOrWhiteSpace(dto.TrackIgnoreRegex) ? null : dto.TrackIgnoreRegex.Trim();
        settings.ArtistIgnoreRegex = string.IsNullOrWhiteSpace(dto.ArtistIgnoreRegex) ? null : dto.ArtistIgnoreRegex.Trim();
        settings.AlbumIgnoreRegex = string.IsNullOrWhiteSpace(dto.AlbumIgnoreRegex) ? null : dto.AlbumIgnoreRegex.Trim();
        _settings.Update(settings);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<UserSettingsDto>.Ok(settings.ToDto());
    }

    public async Task<Result<PagedResponse<AdminUserListItem>>> GetUsersAsync(int page, int pageSize, string? search, CancellationToken cancellationToken = default)
    {
        page = AppConstants.ClampPage(page);
        pageSize = AppConstants.ClampPageSize(pageSize);
        var (items, total) = await _users.GetAdminListAsync(page, pageSize, search?.Trim(), cancellationToken);
        return Result<PagedResponse<AdminUserListItem>>.Ok(
            new PagedResponse<AdminUserListItem>(items, page, pageSize, total));
    }

    public async Task<Result<AdminUserDetail>> GetUserDetailAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null)
            return Result<AdminUserDetail>.NotFound("User not found.");

        var settings = await _settings.GetByUserIdAsync(user.Id, cancellationToken);
        var count = await _scrobbles.CountAsync(user.Id, cancellationToken: cancellationToken);

        return Result<AdminUserDetail>.Ok(new AdminUserDetail(
            user.Id, user.Username, user.Email, Mappers.ToUnix(user.CreatedAt),
            user.IsAdmin, user.IsDisabled, count,
            settings?.ProfileVisibility ?? ProfileVisibility.Public,
            settings?.Theme ?? Theme.System));
    }

    public async Task<Result> SetDisabledAsync(Guid userId, bool disabled, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null)
            return Result.NotFound("User not found.");

        user.IsDisabled = disabled;
        _users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} {State} by admin", userId, disabled ? "disabled" : "enabled");
        return Result.Ok();
    }

    public async Task<Result> SetAdminAsync(Guid userId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null)
            return Result.NotFound("User not found.");

        user.IsAdmin = isAdmin;
        _users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    public async Task<Result<TokenResponse>> RegenerateUserTokenAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null)
            return Result<TokenResponse>.NotFound("User not found.");

        user.ApiToken = _tokenGenerator.GenerateApiToken();
        _users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Admin regenerated API token for user {UserId}", userId);
        return Result<TokenResponse>.Ok(new TokenResponse(user.ApiToken));
    }

    public async Task<Result> DeleteUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null)
            return Result.NotFound("User not found.");

        _users.Remove(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Admin deleted user {UserId} ({Username}) and all their data", userId, user.Username);
        return Result.Ok();
    }
}
