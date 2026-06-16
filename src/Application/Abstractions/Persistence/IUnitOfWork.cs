namespace Scrobblint.Application.Abstractions.Persistence;

/// <summary>
/// Commits all pending changes made through the repositories as a single unit.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
