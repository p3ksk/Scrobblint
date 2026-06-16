using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Scrobblint.Api;
using Scrobblint.Application;
using Scrobblint.Infrastructure;
using Scrobblint.Infrastructure.Persistence;
using Scrobblint.Web.Authentication;
using Scrobblint.Web.Components;

var builder = WebApplication.CreateBuilder(args);

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
