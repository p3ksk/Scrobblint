using Microsoft.EntityFrameworkCore;
using Scrobblint.Application.Abstractions.Persistence;

namespace Scrobblint.Infrastructure.Persistence;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly ScrobblintDbContext _context;

    public UnitOfWork(ScrobblintDbContext context) => _context = context;

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var result = await _context.SaveChangesAsync(cancellationToken);

        // Stop tracking the just-saved entities. Reads run on their own short-lived contexts, so the
        // write context only ever needs to hold the entities of the current operation; clearing keeps
        // it from accumulating instances that would clash when a later Update re-attaches a detached
        // entity returned by a read. (Per-request write contexts are short-lived, so this is harmless.)
        _context.ChangeTracker.Clear();
        return result;
    }
}
