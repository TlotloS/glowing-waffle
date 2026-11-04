using OT.Assessment.Core.Models;

namespace OT.Assessment.Core.Interfaces;

public interface ICasinoWagerPublisher
{
    Task PublishAsync(CasinoWager wager, CancellationToken cancellationToken);
}
