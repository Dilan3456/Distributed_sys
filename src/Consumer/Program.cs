using Consumer;
using Prometheus;
using Shared;

var builder = WebApplication.CreateBuilder(args);

// Same config-binding pattern as the Producer.
var kafka = builder.Configuration.GetSection("Kafka").Get<KafkaSettings>()
            ?? new KafkaSettings();
builder.Services.AddSingleton(kafka);

// Register the Kafka consume loop as a hosted background service.
builder.Services.AddHostedService<OrderConsumer>();

var app = builder.Build();

// Kubernetes probes.
app.MapGet("/healthz", () => Results.Ok("healthy"));
app.MapGet("/readyz", () => Results.Ok("ready"));

// Prometheus scrape endpoint -> /metrics
app.UseMetricServer();

app.Run();
