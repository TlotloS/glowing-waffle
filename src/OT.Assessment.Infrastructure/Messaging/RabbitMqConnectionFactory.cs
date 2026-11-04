using RabbitMQ.Client;

namespace OT.Assessment.Infrastructure.Messaging;

// Shared by the publisher and the consumer so both connect and declare the queues the same way.
public static class RabbitMqConnectionFactory
{
    public static async Task<IConnection> CreateConnectionAsync(RabbitMqOptions options, string clientName, CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = options.HostName,
            Port = options.Port,
            UserName = options.UserName,
            Password = options.Password,
            ClientProvidedName = clientName
        };

        IConnection connection = await factory.CreateConnectionAsync(cancellationToken);
        return connection;
    }

    public static async Task DeclareQueuesAsync(IChannel channel, RabbitMqOptions options, CancellationToken cancellationToken)
    {
        await channel.QueueDeclareAsync(
            queue: options.DeadLetterQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        // A rejected message on the main queue goes to the dead letter queue through the default exchange.
        var mainQueueArguments = new Dictionary<string, object?>
        {
            { "x-dead-letter-exchange", "" },
            { "x-dead-letter-routing-key", options.DeadLetterQueueName }
        };

        await channel.QueueDeclareAsync(
            queue: options.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: mainQueueArguments,
            cancellationToken: cancellationToken);
    }
}
