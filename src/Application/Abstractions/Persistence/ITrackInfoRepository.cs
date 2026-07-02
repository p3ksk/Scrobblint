using Scrobblint.Domain.Entities;

namespace Scrobblint.Application.Abstractions.Persistence;

public interface ITrackInfoRepository
{
    Task<TrackInfo?> GetAsync(string artistKey, string trackKey, CancellationToken cancellationToken = default);
    Task AddAsync(TrackInfo info, CancellationToken cancellationToken = default);
    Task<int> CountAsync(CancellationToken cancellationToken = default);
    Task<TrackInfo?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<TrackInfo> Items, int Total)> ListAsync(int page, int pageSize, string? search, CancellationToken cancellationToken = default);
    void Update(TrackInfo info);
    void Delete(TrackInfo info);
}
