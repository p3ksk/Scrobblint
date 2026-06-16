using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.OpenApi.Models;
using Scrobblint.Api.Authentication;

namespace Scrobblint.Api;

public static class ApiServiceCollectionExtensions
{
    /// <summary>
    /// Adds the ListenBrainz-style "Token" authentication scheme. Call after <c>AddAuthentication</c>.
    /// Works in both hosts: the API host makes it the default; the Web host keeps cookies default and
    /// adds this for the REST surface.
    /// </summary>
    public static AuthenticationBuilder AddTokenAuthentication(this AuthenticationBuilder builder) =>
        builder.AddScheme<AuthenticationSchemeOptions, TokenAuthenticationHandler>(
            TokenAuthenticationDefaults.Scheme, _ => { });

    /// <summary>
    /// Registers the REST API cross-cutting concerns: authorization policies, rate limiting and
    /// (optionally) Swagger. Authentication schemes are configured by the caller.
    /// </summary>
    public static IServiceCollection AddScrobblintApiServices(this IServiceCollection services, bool includeSwagger = true)
    {
        // Policies pin themselves to the Token scheme so the REST surface behaves identically
        // regardless of the host's default scheme (cookies in the Web app).
        services.AddAuthorizationBuilder()
            .AddPolicy(AuthorizationPolicies.TokenAuthenticated, policy =>
            {
                policy.AddAuthenticationSchemes(TokenAuthenticationDefaults.Scheme);
                policy.RequireAuthenticatedUser();
            })
            .AddPolicy(AuthorizationPolicies.AdminOnly, policy =>
            {
                policy.AddAuthenticationSchemes(TokenAuthenticationDefaults.Scheme);
                policy.RequireAuthenticatedUser();
                policy.RequireRole(TokenAuthenticationDefaults.AdminRole);
            });

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(PartitionKey(context), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 300,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));

            options.AddPolicy(RateLimitingPolicies.Auth, context =>
                RateLimitPartition.GetFixedWindowLimiter(PartitionKey(context), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));
        });

        if (includeSwagger)
        {
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "Scrobblint API",
                    Version = "v1",
                    Description = "A lightweight, self-hosted scrobbling service (ListenBrainz-style token auth)."
                });

                var scheme = new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.ApiKey,
                    In = ParameterLocation.Header,
                    Description = "ListenBrainz-style token. Format: `Token YOUR_TOKEN`",
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Token" }
                };
                options.AddSecurityDefinition("Token", scheme);
                options.AddSecurityRequirement(new OpenApiSecurityRequirement { [scheme] = Array.Empty<string>() });
            });
        }

        return services;
    }

    private static string PartitionKey(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("Authorization", out var auth) &&
            auth.ToString().StartsWith(TokenAuthenticationDefaults.HeaderPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return "token:" + auth.ToString()[TokenAuthenticationDefaults.HeaderPrefix.Length..].Trim();
        }
        return "ip:" + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown");
    }
}
