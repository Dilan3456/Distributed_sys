namespace Shared;

/// <summary>
/// The event that flows through Kafka from Producer -> Consumer.
/// Kept deliberately small so the focus stays on the distributed-systems
/// behaviour (partitioning, consumer groups, rebalancing) rather than logic.
/// </summary>
public record OrderEvent
{
    public string OrderId { get; init; } = Guid.NewGuid().ToString();
    public string Customer { get; init; } = "anonymous";
    public decimal Amount { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Strongly-typed view of the "Kafka" section in appsettings.json.
/// Bound at startup so config lives outside code (twelve-factor).
/// </summary>
public class KafkaSettings
{
    public string BootstrapServers { get; set; } = "localhost:9092";
    public string Topic { get; set; } = "orders";
    public string ConsumerGroup { get; set; } = "order-consumers";
}
