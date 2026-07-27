using System.Text.Json;
using Confluent.Kafka;
using Prometheus;
using Shared;

namespace Consumer;

/// <summary>
/// Long-running background service that subscribes to the Kafka topic and
/// processes OrderEvents. This is the heart of the "consumer group" behaviour:
/// run several replicas and Kafka splits partitions across them, rebalancing
/// automatically when replicas come and go.
/// </summary>
public class OrderConsumer : BackgroundService
{
    private readonly KafkaSettings _cfg;
    private readonly ILogger<OrderConsumer> _log;

    // Prometheus metrics, scraped at /metrics -> visible in Grafana/Kiali.
    private static readonly Counter Processed = Metrics
        .CreateCounter("orders_processed_total", "Total orders processed");
    private static readonly Counter Failed = Metrics
        .CreateCounter("orders_failed_total", "Total orders that failed");

    public OrderConsumer(KafkaSettings cfg, ILogger<OrderConsumer> log)
    {
        _cfg = cfg;
        _log = log;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Run the blocking consume loop on a background thread so the host
        // (and the /metrics + /healthz web endpoints) stays responsive.
        return Task.Run(() => ConsumeLoop(stoppingToken), stoppingToken);
    }

    private void ConsumeLoop(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _cfg.BootstrapServers,
            GroupId = _cfg.ConsumerGroup,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false      // commit manually after processing
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(_cfg.Topic);
        _log.LogInformation("Subscribed to {Topic} as group {Group}",
            _cfg.Topic, _cfg.ConsumerGroup);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var cr = consumer.Consume(stoppingToken);
                try
                {
                    var order = JsonSerializer.Deserialize<OrderEvent>(cr.Message.Value);
                    _log.LogInformation(
                        "Processed {OrderId} amount={Amount} p{Partition}@{Offset}",
                        order?.OrderId, order?.Amount,
                        cr.Partition.Value, cr.Offset.Value);

                    // Commit only after successful processing -> at-least-once.
                    consumer.Commit(cr);
                    Processed.Inc();
                }
                catch (Exception ex)
                {
                    Failed.Inc();
                    _log.LogError(ex, "Failed to process message at {Offset}",
                        cr.Offset.Value);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _log.LogInformation("Consumer shutting down cleanly");
        }
        finally
        {
            consumer.Close();   // triggers a final rebalance for the group
        }
    }
}
