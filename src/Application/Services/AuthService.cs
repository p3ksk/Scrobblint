using Microsoft.Extensions.Logging;
using Scrobblint.Application.Abstractions;
using Scrobblint.Application.Abstractions.Persistence;
using Scrobblint.Application.Abstractions.Security;
using Scrobblint.Application.Common;
using Scrobblint.Domain.Entities;
using Scrobblint.Shared.Auth;

namespace Scrobblint.Application.Services;

public sealed class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly IUserSettingsRepository _settings;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenGenerator _tokenGenerator;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository users,
        IUserSettingsRepository settings,
        IPasswordHasher passwordHasher,
        ITokenGenerator tokenGenerator,
        IUnitOfWork unitOfWork,
        IClock clock,
        ILogger<AuthService> logger)
    {
        _users = users;
        _settings = settings;
        _passwordHasher = passwordHasher;
        _tokenGenerator = tokenGenerator;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<RegisterResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var username = request.Username?.Trim() ?? string.Empty;
        var email = request.Email?.Trim() ?? string.Empty;

        var validation = new ValidationBuilder();
        validation.Required(nameof(request.Username), username);
        validation.Length(nameof(request.Username), username, AppConstants.UsernameMinLength, AppConstants.UsernameMaxLength);
        validation.AddIf(!string.IsNullOrEmpty(username) && !IsValidUsername(username),
            nameof(request.Username), "Username may only contain letters, digits, '.', '_' and '-'.");
        validation.Required(nameof(request.Email), email);
        validation.AddIf(!string.IsNullOrEmpty(email) && !IsValidEmail(email),
            nameof(request.Email), "A valid e-mail address is required.");
        validation.Required(nameof(request.Password), request.Password);
        validation.Length(nameof(request.Password), request.Password, AppConstants.PasswordMinLength, AppConstants.PasswordMaxLength);

        if (validation.HasErrors)
            return Result<RegisterResponse>.Invalid(validation.Build());

        if (await _users.UsernameExistsAsync(username, cancellationToken))
            return Result<RegisterResponse>.Conflict("That username is already taken.");

        if (await _users.EmailExistsAsync(email, cancellationToken))
            return Result<RegisterResponse>.Conflict("That e-mail address is already registered.");

        var user = new User
        {
            Username = username,
            Email = email,
            PasswordHash = _passwordHasher.Hash(request.Password),
            ApiToken = _tokenGenerator.GenerateApiToken(),
            CreatedAt = _clock.UtcNow,
            IsAdmin = false,
            IsDisabled = false
        };

        await _users.AddAsync(user, cancellationToken);
        await _settings.AddAsync(new UserSettings { UserId = user.Id }, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Registered new user {Username} ({UserId})", user.Username, user.Id);
        return Result<RegisterResponse>.Ok(new RegisterResponse(user.Id, user.Username, user.Email, user.ApiToken));
    }

    public async Task<Result<TokenResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var credentials = await ValidateCredentialsAsync(request.UsernameOrEmail, request.Password, cancellationToken);
        if (credentials.Failed)
            return Result<TokenResponse>.Fail(credentials.Error, credentials.Message ?? "Login failed.");

        return Result<TokenResponse>.Ok(new TokenResponse(credentials.Value!.ApiToken));
    }

    public async Task<Result<User>> ValidateCredentialsAsync(string usernameOrEmail, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(usernameOrEmail) || string.IsNullOrWhiteSpace(password))
            return Result<User>.Unauthorized("Invalid credentials.");

        var user = await _users.GetByUsernameOrEmailAsync(usernameOrEmail.Trim(), cancellationToken);

        // Always run a verify to reduce trivial user-enumeration timing differences.
        var hash = user?.PasswordHash ?? string.Empty;
        var passwordOk = !string.IsNullOrEmpty(hash) && _passwordHasher.Verify(hash, password);

        if (user is null || !passwordOk)
            return Result<User>.Unauthorized("Invalid credentials.");

        if (user.IsDisabled)
            return Result<User>.Forbidden("This account has been disabled.");

        return Result<User>.Ok(user);
    }

    public async Task<Result<TokenResponse>> GetTokenAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken);
        return user is null
            ? Result<TokenResponse>.NotFound("User not found.")
            : Result<TokenResponse>.Ok(new TokenResponse(user.ApiToken));
    }

    public async Task<Result<TokenResponse>> RegenerateTokenAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null)
            return Result<TokenResponse>.NotFound("User not found.");

        user.ApiToken = _tokenGenerator.GenerateApiToken();
        _users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Regenerated API token for user {UserId}", userId);
        return Result<TokenResponse>.Ok(new TokenResponse(user.ApiToken));
    }

    public async Task<User?> AuthenticateTokenAsync(string apiToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiToken))
            return null;

        var user = await _users.GetByApiTokenAsync(apiToken.Trim(), cancellationToken);
        if (user is null || user.IsDisabled)
            return null;

        return user;
    }

    private static bool IsValidUsername(string username) =>
        username.All(c => char.IsLetterOrDigit(c) || c is '.' or '_' or '-');

    private static bool IsValidEmail(string email)
    {
        // Deliberately lenient: ensures a single '@' with text either side and a dot in the domain.
        var at = email.IndexOf('@');
        if (at <= 0 || at != email.LastIndexOf('@') || at == email.Length - 1) return false;
        var domain = email[(at + 1)..];
        return domain.Contains('.') && !domain.StartsWith('.') && !domain.EndsWith('.');
    }
}
