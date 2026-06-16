namespace Scrobblint.Application.Abstractions.Security;

/// <summary>
/// Hashes and verifies passwords. The default implementation wraps ASP.NET Core's
/// <c>PasswordHasher</c> (PBKDF2) — see the Infrastructure layer.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);

    /// <summary>Returns true when <paramref name="password"/> matches <paramref name="passwordHash"/>.</summary>
    bool Verify(string passwordHash, string password);
}
