using Scrobblint.Application.Abstractions.Persistence;

namespace Scrobblint.Infrastructure.Persistence;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly ScrobblintDbContext _context;

    public UnitOfWork(ScrobblintDbContext context) => _context = context;

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
