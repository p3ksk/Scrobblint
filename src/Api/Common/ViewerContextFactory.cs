using System.Security.Claims;
using Scrobblint.Api.Authentication;
using Scrobblint.Application.Common;

namespace Scrobblint.Api.Common;

public static class ViewerContextFactory
{
    /// <summary>Builds a <see cref="ViewerContext"/> from the current principal (anonymous when no token).</summary>
    public static ViewerContext From(ClaimsPrincipal principal)
    {
        var userId = principal.GetUserId();
        return userId is null ? ViewerContext.Anonymous : new ViewerContext(userId, principal.IsAdmin());
    }
}
