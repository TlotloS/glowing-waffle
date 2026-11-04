using OT.Assessment.Core.Models;

namespace OT.Assessment.Core.Interfaces;

public interface IPlayerRepository
{
    Task<IReadOnlyList<TopSpender>> GetTopSpendersAsync(int count, CancellationToken cancellationToken);
}
