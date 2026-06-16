using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Scrobblint.Api.Authentication;
using Scrobblint.Domain.Entities;

namespace Scrobblint.Web.Authentication;

/// <summary>
/// Issues and clears the browser auth cookie. Mirrors the API token claims so the same
/// authorization policies (admin role) apply across the UI and the REST surface.
/// </summary>
public sealed class CookieSignInService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CookieSignInService(IHttpContextAccessor httpContextAccessor) =>
        _httpContextAccessor = httpContextAccessor;

    public static ClaimsPrincipal BuildPrincipal(User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
        };
        if (user.IsAdmin)
            claims.Add(new Claim(ClaimTypes.Role, TokenAuthenticationDefaults.AdminRole));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return new ClaimsPrincipal(identity);
    }

    public async Task SignInAsync(User user)
    {
        var context = _httpContextAccessor.HttpContext
                      ?? throw new InvalidOperationException("No active HttpContext to sign in.");

        var principal = BuildPrincipal(user);
        await context.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = true });
    }

    public async Task SignOutAsync()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context is not null)
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
