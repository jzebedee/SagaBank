using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace SagaBank.Kafka;

public sealed class Consumer : IDisposable
{
    private readonly IOptions<ConsumerConfig> _options;
    private readonly ILogger _logger;

    private readonly ConcurrentDictionary<string, Lazy<IConsumer<string, string>>> _consumers = new();

    public Consumer(ILogger<Consumer> logger, IOptions<ConsumerConfig> options)
    {
        _logger = logger;
        _options = options;
    }

    public void Dispose()
    {
        var consumers = _consumers.Values;
        foreach (var consumer in consumers)
        {
            if (consumer is { IsValueCreated: true, Value: IConsumer<string, string> c })
            {
                c.Close();
                c.Dispose();
            }
        }
    }

    public void ConsumeDummyData(string topic)
    {
        var consumer = _consumers.GetOrAdd(topic, t => new(() =>
        {
            var c = new ConsumerBuilder<string, string>(_options.Value).Build();
            c.Subscribe(t);
            return c;
        })).Value;

        if (consumer.Consume(TimeSpan.FromSeconds(10)) is ConsumeResult<string, string> cr)
        {
            _logger.LogInformation("Consumed event from topic {topic} with key {key,-10} and value {value}", topic, cr.Message.Key, cr.Message.Value);
            return;
        }

        _logger.LogWarning("Failed to consume any event");
    }
}