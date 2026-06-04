using System.Text.Json;
using DevIa.Application.Abstractions.Messaging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace DevIa.Infrastructure.Messaging;

/// <summary>RabbitMQ implementation of <see cref="IReviewJobQueue"/> (replaces the B1 stub).</summary>
public sealed class RabbitMqReviewJobQueue(RabbitMqConnection connection, IOptions<RabbitMqOptions> options)
    : IReviewJobQueue
{
    private readonly string _queue = options.Value.QueueName;

    public async Task EnqueueAsync(ReviewQueuedMessage message, CancellationToken cancellationToken = default)
    {
        await using var channel = await connection.CreateChannelAsync(cancellationToken);
        await channel.QueueDeclareAsync(
            queue: _queue, durable: true, exclusive: false, autoDelete: false,
            cancellationToken: cancellationToken);

        var body = JsonSerializer.SerializeToUtf8Bytes(message);
        var properties = new BasicProperties { Persistent = true, ContentType = "application/json" };

        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: _queue,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);
    }
}
