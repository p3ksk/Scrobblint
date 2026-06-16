using Scrobblint.Application.Common;
using Scrobblint.Domain.Entities;
using Scrobblint.Shared.Auth;

namespace Scrobblint.Application.Services;

/// <summary>
/// Registration, credential validation and API-token lifecycle.
/// </summary>
public interface IAuthService
{
    Task<Result<RegisterResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);

    /// <summary>Validates credentials and returns the account's current API token.</summary>
    Task<Result<TokenResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    /// <summary>Validates credentials and returns the user (used by the Blazor cookie login).</summary>
    Task<Result<User>> ValidateCredentialsAsync(string usernameOrEmail, string password, CancellationToken cancellationToken = default);

    /// <summary>Returns the user's current API token (for display on the token-management page).</summary>
    Task<Result<TokenResponse>> GetTokenAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Issues a brand-new API token for the user, invalidating the previous one.</summary>
    Task<Result<TokenResponse>> RegenerateTokenAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the active user that owns <paramref name="apiToken"/>, or null when the token is
    /// unknown or the account is disabled. Used by the API authentication handler.
    /// </summary>
    Task<User?> AuthenticateTokenAsync(string apiToken, CancellationToken cancellationToken = default);
}
