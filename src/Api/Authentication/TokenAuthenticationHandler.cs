using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Scrobblint.Application.Services;

namespace Scrobblint.Api.Authentication;

public static class TokenAuthenticationDefaults
{
    public const string Scheme = "Token";

    /// <summary>The ListenBrainz-style prefix: <c>Authorization: Token YOUR_TOKEN</c>.</summary>
    public const string HeaderPrefix = "Token ";

    /// <summary>Custom claim type carrying the admin flag.</summary>
    public const string AdminClaim = "scrobblint:admin";
    public const string AdminRole = "Admin";
}

/// <summary>
/// Authenticates API requests by resolving the user that owns the bearer API token supplied in the
/// <c>Authorization: Token …</c> header.
/// </summary>
public sealed class TokenAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TokenAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var header) || header.Count == 0)
            return AuthenticateResult.NoResult();

        var raw = header.ToString();
        if (string.IsNullOrWhiteSpace(raw) ||
            !raw.StartsWith(TokenAuthenticationDefaults.HeaderPrefix, StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.NoResult();

        var token = raw[TokenAuthenticationDefaults.HeaderPrefix.Length..].Trim();
        if (string.IsNullOrEmpty(token))
            return AuthenticateResult.Fail("Empty API token.");

        var authService = Context.RequestServices.GetRequiredService<IAuthService>();
        var user = await authService.AuthenticateTokenAsync(token, Context.RequestAborted);
        if (user is null)
            return AuthenticateResult.Fail("Invalid or disabled API token.");

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(TokenAuthenticationDefaults.AdminClaim, user.IsAdmin ? "true" : "false")
        };
        if (user.IsAdmin)
            claims.Add(new Claim(ClaimTypes.Role, TokenAuthenticationDefaults.AdminRole));

        var identity = new ClaimsIdentity(claims, TokenAuthenticationDefaults.Scheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, TokenAuthenticationDefaults.Scheme);
        return AuthenticateResult.Success(ticket);
    }
}
