namespace Scrobblint.Application.Abstractions.Security;

/// <summary>
/// Produces cryptographically secure, URL-safe API tokens.
/// </summary>
public interface ITokenGenerator
{
    string GenerateApiToken();
}
