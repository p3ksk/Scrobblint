using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace Scrobblint.Web.Authentication;

/// <summary>
/// Supplies the Blazor authentication state from the current request's <see cref="ClaimsPrincipal"/>.
/// Because the UI is rendered with static SSR, the <see cref="HttpContext"/> is available per request.
/// </summary>
public sealed class ServerAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ServerAuthenticationStateProvider(IHttpContextAccessor httpContextAccessor) =>
        _httpContextAccessor = httpContextAccessor;

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var principal = _httpContextAccessor.HttpContext?.User
                        ?? new ClaimsPrincipal(new ClaimsIdentity());
        return Task.FromResult(new AuthenticationState(principal));
    }
}
