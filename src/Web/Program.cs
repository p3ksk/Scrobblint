using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Components.Authorization;
using Scrobblint.Api;
using Scrobblint.Api.Authentication;
using Scrobblint.Application;
using Scrobblint.Application.Abstractions.Persistence;
using Scrobblint.Infrastructure;
using Scrobblint.Infrastructure.Persistence;
using Scrobblint.Web.Authentication;
using Scrobblint.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// --- Data protection: persist the key ring so antiforgery tokens (and the auth cookie)
// survive process restarts. Without this the keys are ephemeral and tokens minted by a
// previous process can no longer be decrypted ("key not found in the key ring").
var keysDir = builder.Environment.IsDevelopment()
    ? Path.Combine(builder.Environment.ContentRootPath, "..", "data", "keys")
    : "/data/keys";
Directory.CreateDirectory(keysDir);

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysDir))
    .SetApplicationName("Scrobblint");

// --- Razor components (static server-side rendering) -----------------------
builder.Services.AddRazorComponents();

// --- Application + persistence --------------------------------------------
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructure(builder.Configuration);

// --- Authentication: cookies for the browser UI, token for the REST API ----
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/denied";
        options.LogoutPath = "/account/logout";
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.SlidingExpiration = true;
        options.Cookie.Name = "scrobblint.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;

        // Reject cookies whose user no longer exists or has been disabled (e.g. a stale session
        // after the database was reset), so a dead session can't slip through and cause errors.
        options.Events = new CookieAuthenticationEvents
        {
            OnValidatePrincipal = async ctx =>
            {
                var userId = ctx.Principal?.GetUserId();
                if (userId is null) { ctx.RejectPrincipal(); return; }

                // Resolve from a fresh scope to avoid DbContext concurrency with Blazor SSR rendering.
                using var scope = ctx.HttpContext.RequestServices.CreateScope();
                var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
                var user = await users.GetByIdAsync(userId.Value, ctx.HttpContext.RequestAborted);

                if (user is null || user.IsDisabled)
                {
                    ctx.RejectPrincipal();
                    await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                }
            }
        };
    })
    .AddTokenAuthentication();

builder.Services.AddScrobblintApiServices(includeSwagger: true);

builder.Services.AddHttpContextAccessor();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, ServerAuthenticationStateProvider>();
builder.Services.AddScoped<CookieSignInService>();

var app = builder.Build();

// --- Apply migrations + seed admin on start-up ----------------------------
await DatabaseInitializer.InitializeAsync(app.Services, app.Lifetime.ApplicationStopping);

// --- Middleware pipeline --------------------------------------------------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Scrobblint API v1"));
}

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>();

// Form-post endpoints backing the login / register / logout pages and the authenticated UI.
app.MapAccountEndpoints();
app.MapUiFormEndpoints();

// Expose the REST API from the same host (token-authenticated) so scrobble clients
// can target a single self-hosted deployment.
app.MapScrobblintApi();

app.Run();
