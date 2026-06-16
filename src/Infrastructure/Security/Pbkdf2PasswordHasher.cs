using Microsoft.AspNetCore.Identity;
using Scrobblint.Application.Abstractions.Security;

namespace Scrobblint.Infrastructure.Security;

/// <summary>
/// Adapts ASP.NET Core's <see cref="PasswordHasher{TUser}"/> (PBKDF2) to our framework-free
/// <see cref="IPasswordHasher"/> abstraction. We do not pull in full ASP.NET Identity — just the
/// well-vetted hashing primitive, as requested.
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    // The TUser parameter is only used by Identity to signal "rehash needed"; a placeholder is fine.
    private static readonly object Placeholder = new();
    private readonly PasswordHasher<object> _inner = new();

    public string Hash(string password) => _inner.HashPassword(Placeholder, password);

    public bool Verify(string passwordHash, string password)
    {
        var result = _inner.VerifyHashedPassword(Placeholder, passwordHash, password);
        return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
