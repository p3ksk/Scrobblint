using System.Security.Claims;

namespace Scrobblint.Api.Authentication;

/// <summary>Convenience accessors for the authenticated user's claims.</summary>
public static class ClaimsPrincipalExtensions
{
    public static Guid? GetUserId(this ClaimsPrincipal principal)
    {
        var raw = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    public static string? GetUsername(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.Name);

    public static bool IsAdmin(this ClaimsPrincipal principal) =>
        principal.IsInRole(TokenAuthenticationDefaults.AdminRole);
}
