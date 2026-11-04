using OT.Assessment.Domain.Models;

namespace OT.Assessment.Domain.Interfaces;

public interface ICasinoWagerRepository
{
    Task InsertBatchAsync(IReadOnlyList<CasinoWager> wagers, CancellationToken cancellationToken);

    Task<PagedResult<PlayerWager>> GetByPlayerAsync(Guid accountId, int page, int pageSize, CancellationToken cancellationToken);
}
