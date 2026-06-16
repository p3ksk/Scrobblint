namespace Scrobblint.Shared.Auth;

/// <summary>POST /api/auth/register</summary>
public sealed record RegisterRequest(string Username, string Email, string Password);

/// <summary>POST /api/auth/login — accepts either the username or the e-mail address.</summary>
public sealed record LoginRequest(string UsernameOrEmail, string Password);

/// <summary>Returned by login and token (re)generation.</summary>
public sealed record TokenResponse(string Token);

/// <summary>Returned by register — the freshly created account plus its first token.</summary>
public sealed record RegisterResponse(Guid Id, string Username, string Email, string Token);
