namespace Scrobblint.Application.Abstractions.Persistence;

/// <summary>
/// Marker abstraction for a storage backend. The concrete implementation (in the
/// Infrastructure layer) knows how to wire up the underlying engine — SQLite, MySQL,
/// and, in future, PostgreSQL / SQL Server / MongoDB.
/// <para>
/// Keeping this contract in the Application layer means the rest of the core code never
/// references a specific database technology; the storage engine is chosen from
/// configuration (<c>Database:Provider</c>) and resolved through dependency injection.
/// </para>
/// </summary>
public interface IDataStorageProvider
{
    /// <summary>The configured provider key, e.g. "SQLite" or "MySQL".</summary>
    string Name { get; }
}
