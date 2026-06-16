using Microsoft.AspNetCore.Antiforgery;
using Scrobblint.Application.Services;
using Scrobblint.Shared.Auth;

namespace Scrobblint.Web.Authentication;

/// <summary>
/// Form-post endpoints that back the static-SSR login / register / logout pages. These run with full
/// control of the response so the auth cookie is written reliably; antiforgery is validated explicitly.
/// </summary>
public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/account");

        group.MapPost("/login", async (
            HttpContext context, IAntiforgery antiforgery, IAuthService auth, CookieSignInService signIn) =>
        {
            if (!await IsValidAntiforgeryAsync(antiforgery, context))
                return Results.BadRequest("Invalid antiforgery token.");

            var form = await context.Request.ReadFormAsync();
            var usernameOrEmail = form["usernameOrEmail"].ToString();
            var password = form["password"].ToString();
            var returnUrl = form["returnUrl"].ToString();

            var result = await auth.ValidateCredentialsAsync(usernameOrEmail, password, context.RequestAborted);
            if (result.Failed)
                return Redirect("/login", result.Message ?? "Login failed.", returnUrl);

            await signIn.SignInAsync(result.Value!);
            return Results.LocalRedirect(SafeReturnUrl(returnUrl, "/dashboard"));
        });

        group.MapPost("/register", async (
            HttpContext context, IAntiforgery antiforgery, IAuthService auth, CookieSignInService signIn) =>
        {
            if (!await IsValidAntiforgeryAsync(antiforgery, context))
                return Results.BadRequest("Invalid antiforgery token.");

            var form = await context.Request.ReadFormAsync();
            var request = new RegisterRequest(
                form["username"].ToString(),
                form["email"].ToString(),
                form["password"].ToString());

            var result = await auth.RegisterAsync(request, context.RequestAborted);
            if (result.Failed)
                return Redirect("/register", result.Message ?? "Registration failed.", null);

            // Auto sign-in after registration, then send the user to their token page.
            var credentials = await auth.ValidateCredentialsAsync(request.Username, request.Password, context.RequestAborted);
            if (credentials.Succeeded)
                await signIn.SignInAsync(credentials.Value!);

            return Results.LocalRedirect("/token");
        });

        group.MapPost("/logout", async (HttpContext context, IAntiforgery antiforgery, CookieSignInService signIn) =>
        {
            if (!await IsValidAntiforgeryAsync(antiforgery, context))
                return Results.BadRequest("Invalid antiforgery token.");

            await signIn.SignOutAsync();
            return Results.LocalRedirect("/");
        });

        return app;
    }

    private static async Task<bool> IsValidAntiforgeryAsync(IAntiforgery antiforgery, HttpContext context)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context);
            return true;
        }
        catch (AntiforgeryValidationException)
        {
            return false;
        }
    }

    private static IResult Redirect(string page, string message, string? returnUrl)
    {
        var url = $"{page}?error={Uri.EscapeDataString(message)}";
        if (!string.IsNullOrEmpty(returnUrl))
            url += $"&returnUrl={Uri.EscapeDataString(returnUrl)}";
        return Results.LocalRedirect(url);
    }

    private static string SafeReturnUrl(string? returnUrl, string fallback)
    {
        if (string.IsNullOrWhiteSpace(returnUrl)) return fallback;
        // Only allow local, relative redirects.
        return returnUrl.StartsWith('/') && !returnUrl.StartsWith("//") ? returnUrl : fallback;
    }
}
