namespace OT.Assessment.Consumer;

public class ConsumerOptions
{
    public const string SectionName = "Consumer";

    // How many unacknowledged messages RabbitMQ will send us at once.
    public ushort PrefetchCount { get; set; } = 1000;

    // The most wagers saved in one stored procedure call.
    public int BatchSize { get; set; } = 500;

    // How long to wait before checking again when there's nothing to save.
    public int IdleDelayMilliseconds { get; set; } = 100;

    public int MaxSaveAttempts { get; set; } = 3;
}
