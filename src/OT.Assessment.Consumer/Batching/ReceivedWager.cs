using OT.Assessment.Core.Models;

namespace OT.Assessment.Consumer.Batching;

// A wager read from the queue, plus the delivery tag needed to acknowledge it once it's saved.
public record ReceivedWager(CasinoWager Wager, ulong DeliveryTag);
