namespace DevIa.Infrastructure.Messaging;

/// <summary>RabbitMQ connection + queue settings (bound from the "RabbitMq" config section).</summary>
public sealed class RabbitMqOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string QueueName { get; set; } = "review-jobs";
}
