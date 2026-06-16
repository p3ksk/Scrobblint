using Scrobblint.Application.Common;
using Scrobblint.Shared.Auth;
using Xunit;

namespace Scrobblint.UnitTests;

public class AuthServiceTests
{
    private static RegisterRequest NewUser(string name = "alice") =>
        new(name, $"{name}@example.com", "supersecret");

    [Fact]
    public async Task Register_creates_user_with_token_and_settings()
    {
        using var host = new TestHost();

        var result = await host.Auth.RegisterAsync(NewUser());

        Assert.True(result.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(result.Value!.Token));
        Assert.Equal("alice", result.Value.Username);

        var settings = await host.Users.GetSettingsAsync(result.Value.Id);
        Assert.True(settings.Succeeded);
    }

    [Theory]
    [InlineData("", "a@b.com", "supersecret")]
    [InlineData("ab", "a@b.com", "supersecret")]          // too short
    [InlineData("alice", "not-an-email", "supersecret")]  // bad email
    [InlineData("alice", "a@b.com", "short")]             // weak password
    public async Task Register_rejects_invalid_input(string username, string email, string password)
    {
        using var host = new TestHost();

        var result = await host.Auth.RegisterAsync(new RegisterRequest(username, email, password));

        Assert.True(result.Failed);
        Assert.Equal(ResultError.Validation, result.Error);
    }

    [Fact]
    public async Task Register_duplicate_username_conflicts()
    {
        using var host = new TestHost();
        await host.Auth.RegisterAsync(NewUser());

        var dupe = await host.Auth.RegisterAsync(new RegisterRequest("ALICE", "other@example.com", "supersecret"));

        Assert.True(dupe.Failed);
        Assert.Equal(ResultError.Conflict, dupe.Error);
    }

    [Fact]
    public async Task Login_with_valid_credentials_returns_token()
    {
        using var host = new TestHost();
        var reg = await host.Auth.RegisterAsync(NewUser());

        var login = await host.Auth.LoginAsync(new LoginRequest("alice", "supersecret"));

        Assert.True(login.Succeeded);
        Assert.Equal(reg.Value!.Token, login.Value!.Token);
    }

    [Fact]
    public async Task Login_with_email_also_works()
    {
        using var host = new TestHost();
        await host.Auth.RegisterAsync(NewUser());

        var login = await host.Auth.LoginAsync(new LoginRequest("alice@example.com", "supersecret"));

        Assert.True(login.Succeeded);
    }

    [Fact]
    public async Task Login_with_wrong_password_is_unauthorized()
    {
        using var host = new TestHost();
        await host.Auth.RegisterAsync(NewUser());

        var login = await host.Auth.LoginAsync(new LoginRequest("alice", "wrong-password"));

        Assert.True(login.Failed);
        Assert.Equal(ResultError.Unauthorized, login.Error);
    }

    [Fact]
    public async Task AuthenticateToken_resolves_user_and_rejects_unknown()
    {
        using var host = new TestHost();
        var reg = await host.Auth.RegisterAsync(NewUser());

        var resolved = await host.Auth.AuthenticateTokenAsync(reg.Value!.Token);
        Assert.NotNull(resolved);
        Assert.Equal("alice", resolved!.Username);

        Assert.Null(await host.Auth.AuthenticateTokenAsync("nope"));
    }

    [Fact]
    public async Task RegenerateToken_replaces_old_token()
    {
        using var host = new TestHost();
        var reg = await host.Auth.RegisterAsync(NewUser());
        var oldToken = reg.Value!.Token;

        var regen = await host.Auth.RegenerateTokenAsync(reg.Value.Id);

        Assert.True(regen.Succeeded);
        Assert.NotEqual(oldToken, regen.Value!.Token);
        Assert.Null(await host.Auth.AuthenticateTokenAsync(oldToken));
        Assert.NotNull(await host.Auth.AuthenticateTokenAsync(regen.Value.Token));
    }
}
