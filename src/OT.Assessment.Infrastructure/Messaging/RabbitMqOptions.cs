namespace OT.Assessment.Infrastructure.Messaging;

public class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string HostName { get; set; } = "localhost";

    public int Port { get; set; } = 5672;

    public string UserName { get; set; } = "guest";

    public string Password { get; set; } = "guest";

    public string QueueName { get; set; } = "casino-wagers";

    // Wagers that keep failing are moved here instead of being lost.
    public string DeadLetterQueueName { get; set; } = "casino-wagers.dead-letter";
}
