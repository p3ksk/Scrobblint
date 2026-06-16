namespace Scrobblint.Api.Authentication;

public static class AuthorizationPolicies
{
    /// <summary>Requires an authenticated API-token caller with the admin role.</summary>
    public const string AdminOnly = "AdminOnly";

    /// <summary>Requires any authenticated API-token caller.</summary>
    public const string TokenAuthenticated = "TokenAuthenticated";
}

public static class RateLimitingPolicies
{
    /// <summary>Stricter limiter for credential endpoints (login / register).</summary>
    public const string Auth = "auth";
}
