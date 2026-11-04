using System.Collections.Concurrent;

namespace OT.Assessment.Consumer.Batching;

// Holds wagers between the RabbitMQ callback that receives them and the loop that saves them.
// ConcurrentQueue keeps them in arrival order and is safe to add to and read from on different threads.
public class WagerBatchBuffer
{
    private readonly ConcurrentQueue<ReceivedWager> _queue = new ConcurrentQueue<ReceivedWager>();

    public void Add(ReceivedWager receivedWager)
    {
        _queue.Enqueue(receivedWager);
    }

    public List<ReceivedWager> TakeBatch(int maxBatchSize)
    {
        var batch = new List<ReceivedWager>();

        while (batch.Count < maxBatchSize)
        {
            bool gotWager = _queue.TryDequeue(out ReceivedWager? receivedWager);
            if (!gotWager)
            {
                break;
            }

            batch.Add(receivedWager!);
        }

        return batch;
    }
}
