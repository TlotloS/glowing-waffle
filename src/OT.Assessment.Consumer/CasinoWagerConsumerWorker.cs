using System.Text.Json;
using Microsoft.Extensions.Options;
using OT.Assessment.Consumer.Batching;
using OT.Assessment.Domain.Interfaces;
using OT.Assessment.Domain.Models;
using OT.Assessment.Infrastructure.Messaging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace OT.Assessment.Consumer;

// Reads wagers from RabbitMQ and saves them to SQL Server in batches.
// A message is only acknowledged after its batch is committed, so a crash means
// redelivery rather than a lost wager. The stored procedure skips wagers it already has.
public class CasinoWagerConsumerWorker : BackgroundService
{
    private readonly RabbitMqOptions _rabbitMqOptions;
    private readonly ConsumerOptions _consumerOptions;
    private readonly ICasinoWagerRepository _wagerRepository;
    private readonly WagerBatchBuffer _buffer;
    private readonly ILogger<CasinoWagerConsumerWorker> _logger;

    private IConnection? _connection;
    private IChannel? _channel;

    public CasinoWagerConsumerWorker(
        IOptions<RabbitMqOptions> rabbitMqOptions,
        IOptions<ConsumerOptions> consumerOptions,
        ICasinoWagerRepository wagerRepository,
        WagerBatchBuffer buffer,
        ILogger<CasinoWagerConsumerWorker> logger)
    {
        _rabbitMqOptions = rabbitMqOptions.Value;
        _consumerOptions = consumerOptions.Value;
        _wagerRepository = wagerRepository;
        _buffer = buffer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await StartConsumingAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            List<ReceivedWager> batch = _buffer.TakeBatch(_consumerOptions.BatchSize);

            if (batch.Count == 0)
            {
                await Task.Delay(_consumerOptions.IdleDelayMilliseconds, stoppingToken);
                continue;
            }

            await SaveBatchAsync(batch, stoppingToken);
        }
    }

    private async Task StartConsumingAsync(CancellationToken cancellationToken)
    {
        _connection = await RabbitMqConnectionFactory.CreateConnectionAsync(_rabbitMqOptions, "OT.Assessment.Consumer", cancellationToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await RabbitMqConnectionFactory.DeclareQueuesAsync(_channel, _rabbitMqOptions, cancellationToken);

        await _channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: _consumerOptions.PrefetchCount,
            global: false,
            cancellationToken: cancellationToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += OnMessageReceivedAsync;

        await _channel.BasicConsumeAsync(
            queue: _rabbitMqOptions.QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: cancellationToken);

        _logger.LogInformation("Consuming wagers from queue {QueueName}", _rabbitMqOptions.QueueName);
    }

    private async Task OnMessageReceivedAsync(object sender, BasicDeliverEventArgs message)
    {
        CasinoWager? wager = null;

        try
        {
            wager = JsonSerializer.Deserialize<CasinoWager>(message.Body.Span);
        }
        catch (JsonException exception)
        {
            _logger.LogError(exception, "Could not read message {DeliveryTag}", message.DeliveryTag);
        }

        if (wager == null)
        {
            // Retrying won't fix a message we can't read, so send it straight to the dead letter queue.
            await _channel!.BasicNackAsync(message.DeliveryTag, multiple: false, requeue: false);
            return;
        }

        var receivedWager = new ReceivedWager(wager, message.DeliveryTag);
        _buffer.Add(receivedWager);
    }

    private async Task SaveBatchAsync(List<ReceivedWager> batch, CancellationToken cancellationToken)
    {
        var wagers = new List<CasinoWager>();
        foreach (ReceivedWager receivedWager in batch)
        {
            wagers.Add(receivedWager.Wager);
        }

        // The batch is in delivery order, so acknowledging the last tag with multiple: true covers the whole batch.
        ulong lastDeliveryTag = batch[batch.Count - 1].DeliveryTag;

        for (int attempt = 1; attempt <= _consumerOptions.MaxSaveAttempts; attempt++)
        {
            try
            {
                await _wagerRepository.InsertBatchAsync(wagers, cancellationToken);
                await _channel!.BasicAckAsync(lastDeliveryTag, multiple: true, cancellationToken);

                _logger.LogInformation("Saved {WagerCount} wagers", wagers.Count);
                return;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(exception, "Saving {WagerCount} wagers failed on attempt {Attempt}", wagers.Count, attempt);

                bool hasAttemptsLeft = attempt < _consumerOptions.MaxSaveAttempts;
                if (hasAttemptsLeft)
                {
                    TimeSpan delay = TimeSpan.FromSeconds(attempt);
                    await Task.Delay(delay, cancellationToken);
                }
            }
        }

        // The batch keeps failing, which usually means one bad wager. Save them one at a time
        // so the good wagers are stored and only the bad ones go to the dead letter queue.
        _logger.LogWarning("Saving {WagerCount} wagers one at a time after {Attempts} failed batch attempts", wagers.Count, _consumerOptions.MaxSaveAttempts);
        await SaveOneByOneAsync(batch, cancellationToken);
    }

    private async Task SaveOneByOneAsync(List<ReceivedWager> batch, CancellationToken cancellationToken)
    {
        foreach (ReceivedWager receivedWager in batch)
        {
            var singleWager = new List<CasinoWager> { receivedWager.Wager };

            try
            {
                await _wagerRepository.InsertBatchAsync(singleWager, cancellationToken);
                await _channel!.BasicAckAsync(receivedWager.DeliveryTag, multiple: false, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "Moving wager {WagerId} to the dead letter queue", receivedWager.Wager.WagerId);
                await _channel!.BasicNackAsync(receivedWager.DeliveryTag, multiple: false, requeue: false, cancellationToken);
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);

        // Anything received but not yet saved is still unacknowledged, so RabbitMQ redelivers it on the next start.
        if (_channel != null)
        {
            await _channel.CloseAsync(cancellationToken);
            _channel.Dispose();
        }

        if (_connection != null)
        {
            await _connection.CloseAsync(cancellationToken);
            _connection.Dispose();
        }
    }
}
