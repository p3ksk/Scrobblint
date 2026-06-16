using System.Security.Cryptography;
using Scrobblint.Application.Abstractions.Security;

namespace Scrobblint.Infrastructure.Security;

/// <summary>
/// Generates 256-bit, URL-safe API tokens using a cryptographically secure RNG.
/// </summary>
public sealed class TokenGenerator : ITokenGenerator
{
    private const int TokenBytes = 32; // 256 bits of entropy

    public string GenerateApiToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(TokenBytes);
        // URL-safe Base64 without padding (43 chars).
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
