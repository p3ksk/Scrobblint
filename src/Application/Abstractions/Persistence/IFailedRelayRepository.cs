using Scrobblint.Domain.Entities;
using Scrobblint.Domain.Enums;

namespace Scrobblint.Application.Abstractions.Persistence;

public interface IFailedRelayRepository
{
    Task AddAsync(FailedRelay failedRelay, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FailedRelay>> GetPendingAtAsync(DateTime at, int limit, CancellationToken cancellationToken = default);
    Task<int> CountByStatusAsync(RelayStatus status, CancellationToken cancellationToken = default);
    void Update(FailedRelay failedRelay);
}
