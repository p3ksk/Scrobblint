using Scrobblint.Domain.Entities;
using Scrobblint.Domain.Enums;
using Scrobblint.Shared.Relay;

namespace Scrobblint.Application.Abstractions.Persistence;

public interface IFailedRelayRepository
{
    Task AddAsync(FailedRelay failedRelay, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FailedRelay>> GetPendingAtAsync(DateTime at, int limit, CancellationToken cancellationToken = default);
    Task<int> CountByStatusAsync(RelayStatus status, CancellationToken cancellationToken = default);
    Task<FailedRelay?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FailedRelay>> GetByUserIdAsync(Guid userId, int limit, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<AdminFailedRelayListItem> Items, int TotalCount)> GetAdminListAsync(
        int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Resets every permanently-failed record back to pending, for immediate retry. Returns the number reset.</summary>
    Task<int> ResetAllFailedAsync(CancellationToken cancellationToken = default);

    void Update(FailedRelay failedRelay);
    void Remove(FailedRelay failedRelay);
}
