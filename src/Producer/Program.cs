using System.Text.Json;
using Confluent.Kafka;
using Shared;

var builder = WebApplication.CreateBuilder(args);

// Bind the "Kafka" section of appsettings.json (overridable via env vars
// like Kafka__BootstrapServers when running in Kubernetes).
var kafka = builder.Configuration.GetSection("Kafka").Get<KafkaSettings>()
            ?? new KafkaSettings();

// One long-lived producer for the app lifetime.
var producerConfig = new ProducerConfig
{
    BootstrapServers = kafka.BootstrapServers,
    Acks = Acks.All            // wait for all in-sync replicas -> durability
};
var producer = new ProducerBuilder<string, string>(producerConfig).Build();
builder.Services.AddSingleton(producer);
builder.Services.AddSingleton(kafka);

var app = builder.Build();

// Health endpoints for Kubernetes liveness/readiness probes.
app.MapGet("/healthz", () => Results.Ok("healthy"));
app.MapGet("/readyz", () => Results.Ok("ready"));

// Publish an order. The OrderId is used as the Kafka key so all events for
// the same order land on the same partition (ordering guarantee per key).
app.MapPost("/orders", async (OrderEvent order,
    IProducer<string, string> p, KafkaSettings cfg, ILogger<Program> log) =>
{
    var value = JsonSerializer.Serialize(order);
    var result = await p.ProduceAsync(cfg.Topic,
        new Message<string, string> { Key = order.OrderId, Value = value });

    log.LogInformation("Published {OrderId} to {Partition}@{Offset}",
        order.OrderId, result.Partition.Value, result.Offset.Value);

    return Results.Ok(new
    {
        order.OrderId,
        partition = result.Partition.Value,
        offset = result.Offset.Value
    });
});

app.Run();
