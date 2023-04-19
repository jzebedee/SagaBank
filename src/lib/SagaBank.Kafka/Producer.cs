using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SagaBank.Kafka;

public sealed class Producer<TKey, TValue> : IDisposable
{
    private readonly IOptions<ProducerConfig> _options;
    private readonly ILogger _logger;

    private readonly Lazy<IProducer<TKey, TValue>> _producer;

    public Producer(ILogger<Producer<TKey, TValue>> logger, IOptions<ProducerConfig> options)
    {
        _logger = logger;
        _options = options;
        _producer = new(() =>
        {
            var builder = new ProducerBuilder<TKey, TValue>(_options.Value);
            builder.SetKeySerializer(KafkaMemoryPackSerializer<TKey>.Instance);
            builder.SetValueSerializer(KafkaMemoryPackSerializer<TValue>.Instance);
            return builder.Build();
        });
    }

    public void Dispose()
    {
        if (_producer is not { IsValueCreated: true, Value: IProducer<TKey, TValue> producer })
        {
            return;
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var token = cts.Token;
        producer.Flush(token);
        producer.Dispose();

        token.ThrowIfCancellationRequested();
    }

    public async Task<DeliveryResult<TKey, TValue>> ProduceAsync(string topic, TKey key, TValue value, CancellationToken cancellationToken = default)
    {
        var producer = _producer.Value;

        var deliveryResult = await producer.ProduceAsync(topic, new Message<TKey, TValue> { Key = key, Value = value }, cancellationToken);
        if (deliveryResult is { Status: PersistenceStatus.Persisted })
        {
            _logger.LogInformation("Produced to topic {topic}: (key:{key,-10},value:{value})", topic, key, value);
        }
        else
        {
            _logger.LogWarning("Failed to produce to topic {topic}: (key:{key,-10},value:{value}), current status: {status}", topic, key, value, deliveryResult.Status);
        }

        return deliveryResult;
    }
}