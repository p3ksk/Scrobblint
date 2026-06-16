namespace Scrobblint.Infrastructure.Configuration;

/// <summary>
/// Bound from "Seed:Admin". When enabled and no admin exists, an administrator account is created
/// on first start-up.
/// </summary>
public sealed class SeedOptions
{
    public const string SectionName = "Seed:Admin";

    public bool Enabled { get; set; }
    public string Username { get; set; } = "admin";
    public string Email { get; set; } = "admin@example.com";
    public string Password { get; set; } = "ChangeMe!123";
}
