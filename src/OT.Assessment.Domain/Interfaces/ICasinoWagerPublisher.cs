using OT.Assessment.Domain.Models;

namespace OT.Assessment.Domain.Interfaces;

public interface ICasinoWagerPublisher
{
    Task PublishAsync(CasinoWager wager, CancellationToken cancellationToken);
}
