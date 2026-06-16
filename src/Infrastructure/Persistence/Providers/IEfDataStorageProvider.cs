using Microsoft.EntityFrameworkCore;
using Scrobblint.Application.Abstractions.Persistence;

namespace Scrobblint.Infrastructure.Persistence.Providers;

/// <summary>
/// An <see cref="IDataStorageProvider"/> backed by an EF Core relational provider.
/// New engines (PostgreSQL, SQL Server, …) are added by implementing this interface and
/// registering them in <see cref="DataStorageProviderFactory"/>.
/// </summary>
public interface IEfDataStorageProvider : IDataStorageProvider
{
    /// <summary>Applies the engine-specific options (connection + migrations assembly) to the context.</summary>
    void Configure(DbContextOptionsBuilder options, string connectionString);

    /// <summary>Whether this provider supports EF migrations (relational engines do; document stores would not).</summary>
    bool SupportsMigrations { get; }
}
