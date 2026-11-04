using OT.Assessment.Domain.Models;

namespace OT.Assessment.Domain.Interfaces;

public interface IPlayerRepository
{
    Task<IReadOnlyList<TopSpender>> GetTopSpendersAsync(int count, CancellationToken cancellationToken);
}
