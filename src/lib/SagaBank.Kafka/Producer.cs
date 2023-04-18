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

    public void Produce(string topic, TKey key, TValue value)
    {
        var producer = _producer.Value;

        producer.Produce(topic, new Message<TKey, TValue> { Key = key, Value = value },
            (deliveryReport) =>
            {
                if (deliveryReport is { Error.Code: ErrorCode.NoError })
                {
                    _logger.LogInformation("Produced event to topic {topic}: key = {key,-10} value = {value}", topic, key, value);
                    return;
                }

                _logger.LogWarning("Failed to deliver message: {reason}", deliveryReport.Error.Reason);
            });
    }
}