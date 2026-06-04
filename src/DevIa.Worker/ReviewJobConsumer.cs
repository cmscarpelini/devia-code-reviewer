using System.Text.Json;
using DevIa.Application.Abstractions.Messaging;
using DevIa.Application.Reviews;
using DevIa.Infrastructure.Messaging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace DevIa.Worker;

/// <summary>
/// Consumes review jobs from RabbitMQ and moves each review from <c>Pending</c> to
/// <c>Processing</c>. The actual review pipeline (LLM) is added in a later slice; for now
/// this proves the async path webhook → queue → worker → persistence.
/// </summary>
public sealed class ReviewJobConsumer(
    RabbitMqConnection connection,
    IOptions<RabbitMqOptions> options,
    IServiceScopeFactory scopeFactory,
    ILogger<ReviewJobConsumer> logger) : BackgroundService
{
    private readonly string _queue = options.Value.QueueName;
    private IChannel? _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _channel = await connection.CreateChannelAsync(stoppingToken);
        await _channel.QueueDeclareAsync(
            queue: _queue, durable: true, exclusive: false, autoDelete: false,
            cancellationToken: stoppingToken);
        await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += OnReceivedAsync;

        await _channel.BasicConsumeAsync(_queue, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);
        logger.LogInformation("Review job consumer listening on '{Queue}'.", _queue);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task OnReceivedAsync(object sender, BasicDeliverEventArgs ea)
    {
        try
        {
            var message = JsonSerializer.Deserialize<ReviewQueuedMessage>(ea.Body.Span);
            if (message is not null)
                await ProcessAsync(message, ea.CancellationToken);

            await _channel!.BasicAckAsync(ea.DeliveryTag, multiple: false, ea.CancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process review job; nacking without requeue.");
            await _channel!.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, ea.CancellationToken);
        }
    }

    private async Task ProcessAsync(ReviewQueuedMessage message, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var processReviewJob = scope.ServiceProvider.GetRequiredService<ProcessReviewJob>();
        await processReviewJob.HandleAsync(message.ReviewId, cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null)
            await _channel.DisposeAsync();
        await base.StopAsync(cancellationToken);
    }
}
