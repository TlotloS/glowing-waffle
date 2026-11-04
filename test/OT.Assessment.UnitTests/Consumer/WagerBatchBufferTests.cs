using OT.Assessment.Consumer.Batching;
using OT.Assessment.Core.Models;

namespace OT.Assessment.UnitTests.Consumer;

public class WagerBatchBufferTests
{
    [Fact]
    public void TakeBatch_WhenEmpty_ReturnsEmptyList()
    {
        var buffer = new WagerBatchBuffer();

        List<ReceivedWager> batch = buffer.TakeBatch(maxBatchSize: 500);

        Assert.Empty(batch);
    }

    [Fact]
    public void TakeBatch_ReturnsWagersInTheOrderTheyArrived()
    {
        var buffer = new WagerBatchBuffer();
        buffer.Add(CreateReceivedWager(deliveryTag: 1));
        buffer.Add(CreateReceivedWager(deliveryTag: 2));
        buffer.Add(CreateReceivedWager(deliveryTag: 3));

        List<ReceivedWager> batch = buffer.TakeBatch(maxBatchSize: 500);

        Assert.Equal(3, batch.Count);
        Assert.Equal(1UL, batch[0].DeliveryTag);
        Assert.Equal(2UL, batch[1].DeliveryTag);
        Assert.Equal(3UL, batch[2].DeliveryTag);
    }

    [Fact]
    public void TakeBatch_TakesNoMoreThanMaxBatchSize_AndLeavesTheRest()
    {
        var buffer = new WagerBatchBuffer();
        for (ulong deliveryTag = 1; deliveryTag <= 5; deliveryTag++)
        {
            buffer.Add(CreateReceivedWager(deliveryTag));
        }

        List<ReceivedWager> firstBatch = buffer.TakeBatch(maxBatchSize: 3);
        List<ReceivedWager> secondBatch = buffer.TakeBatch(maxBatchSize: 3);

        Assert.Equal(3, firstBatch.Count);
        Assert.Equal(2, secondBatch.Count);
        Assert.Equal(4UL, secondBatch[0].DeliveryTag);
    }

    private static ReceivedWager CreateReceivedWager(ulong deliveryTag)
    {
        var wager = new CasinoWager
        {
            WagerId = Guid.NewGuid(),
            AccountId = Guid.NewGuid()
        };

        return new ReceivedWager(wager, deliveryTag);
    }
}
