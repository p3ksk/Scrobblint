using Scrobblint.Api;
using Scrobblint.Api.Authentication;
using Scrobblint.Api.ListenBrainz;
using Scrobblint.Application;
using Scrobblint.Infrastructure;
using Scrobblint.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// --- Services -------------------------------------------------------------
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructure(builder.Configuration);

// The standalone API uses the bearer token scheme as its default.
builder.Services
    .AddAuthentication(TokenAuthenticationDefaults.Scheme)
    .AddTokenAuthentication();
builder.Services.AddScrobblintApiServices(includeSwagger: true);
builder.Services.AddProblemDetails();

var app = builder.Build();

// --- Apply migrations + seed admin on start-up ----------------------------
await DatabaseInitializer.InitializeAsync(app.Services, app.Lifetime.ApplicationStopping);

// --- Middleware pipeline --------------------------------------------------
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Scrobblint API v1"));
}

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapScrobblintApi();
app.MapListenBrainzApi();
app.MapGet("/", () => Results.Ok(new { service = "Scrobblint", status = "ok" })).ExcludeFromDescription();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" })).WithTags("Health");

app.Run();

// Exposed so integration tests can use WebApplicationFactory<Program>.
public partial class Program;
