using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace SagaBank.Kafka;

public sealed class Consumer<TKey, TValue> : IDisposable
{
    private readonly IOptions<ConsumerConfig> _options;
    private readonly ILogger _logger;

    private readonly ConcurrentDictionary<string, Lazy<IConsumer<TKey, TValue>>> _consumers = new();

    public Consumer(ILogger<Consumer<TKey, TValue>> logger, IOptions<ConsumerConfig> options)
    {
        _logger = logger;
        _options = options;
    }

    public void Dispose()
    {
        var consumers = _consumers.Values;
        foreach (var consumer in consumers)
        {
            if (consumer is not { IsValueCreated: true, Value: IConsumer<TKey, TValue> c })
            {
                continue;
            }

            c.Close();
            c.Dispose();
        }
    }

    public Message<TKey, TValue>? Consume(string topic, CancellationToken cancellationToken = default)
    {
        var consumer = GetConsumerForTopic(topic);
        if (consumer.Consume(cancellationToken) is ConsumeResult<TKey, TValue> cr)
        {
            _logger.LogInformation("Consumed event from topic {topic} with key {key,-10} and value {value}", topic, cr.Message.Key, cr.Message.Value);
            return cr.Message;
        }

        _logger.LogWarning("Failed to consume any event");
        return null;
    }

    private IConsumer<TKey,TValue> GetConsumerForTopic(string topic)
        => _consumers.GetOrAdd(topic, t => new(() => CreateConsumer(t))).Value;

    private IConsumer<TKey, TValue> CreateConsumer(string topic)
    {
        var builder = new ConsumerBuilder<TKey, TValue>(_options.Value);
        builder.SetKeyDeserializer(KafkaMemoryPackDeserializer<TKey>.Instance);
        builder.SetValueDeserializer(KafkaMemoryPackDeserializer<TValue>.Instance);

        var c = builder.Build();
        c.Subscribe(topic);
        return c;
    }
}