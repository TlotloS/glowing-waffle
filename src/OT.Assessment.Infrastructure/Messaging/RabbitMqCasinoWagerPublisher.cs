using System.Text.Json;
using Microsoft.Extensions.Options;
using OT.Assessment.Core.Interfaces;
using OT.Assessment.Core.Models;
using RabbitMQ.Client;

namespace OT.Assessment.Infrastructure.Messaging;

// Registered as a singleton so the whole API shares one connection and one channel.
// A channel isn't safe to publish on from many threads at once, so publishes take turns using a semaphore.
public sealed class RabbitMqCasinoWagerPublisher : ICasinoWagerPublisher, IAsyncDisposable
{
    private readonly RabbitMqOptions _options;
    private readonly SemaphoreSlim _publishLock = new SemaphoreSlim(1, 1);

    private IConnection? _connection;
    private IChannel? _channel;

    public RabbitMqCasinoWagerPublisher(IOptions<RabbitMqOptions> options)
    {
        _options = options.Value;
    }

    public async Task PublishAsync(CasinoWager wager, CancellationToken cancellationToken)
    {
        byte[] body = JsonSerializer.SerializeToUtf8Bytes(wager);

        // Persistent messages are written to disk, so they survive a RabbitMQ restart.
        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            MessageId = wager.WagerId.ToString()
        };

        await _publishLock.WaitAsync(cancellationToken);
        try
        {
            IChannel channel = await GetChannelAsync(cancellationToken);

            // With publisher confirms turned on, this only returns once RabbitMQ has accepted the message.
            await channel.BasicPublishAsync(
                exchange: "",
                routingKey: _options.QueueName,
                mandatory: true,
                basicProperties: properties,
                body: body,
                cancellationToken: cancellationToken);
        }
        finally
        {
            _publishLock.Release();
        }
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken cancellationToken)
    {
        bool channelIsOpen = _channel != null && _channel.IsOpen;
        if (channelIsOpen)
        {
            return _channel!;
        }

        bool connectionIsOpen = _connection != null && _connection.IsOpen;
        if (!connectionIsOpen)
        {
            _connection = await RabbitMqConnectionFactory.CreateConnectionAsync(_options, "OT.Assessment.App", cancellationToken);
        }

        var channelOptions = new CreateChannelOptions(
            publisherConfirmationsEnabled: true,
            publisherConfirmationTrackingEnabled: true);

        _channel = await _connection!.CreateChannelAsync(channelOptions, cancellationToken);

        await RabbitMqConnectionFactory.DeclareQueuesAsync(_channel, _options, cancellationToken);

        return _channel;
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel != null)
        {
            await _channel.CloseAsync();
            _channel.Dispose();
        }

        if (_connection != null)
        {
            await _connection.CloseAsync();
            _connection.Dispose();
        }

        _publishLock.Dispose();
    }
}
