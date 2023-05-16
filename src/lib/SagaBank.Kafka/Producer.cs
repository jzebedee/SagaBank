using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using SagaBank.Kafka.Serializers;

namespace SagaBank.Kafka;

public sealed class Producer<TKey, TValue> : IDisposable
{
    private readonly ILogger _logger;

    private readonly Lazy<IProducer<TKey, TValue>> _producer;

    public Producer(ILogger<Producer<TKey, TValue>> logger, ProducerConfig options)
    {
        _logger = logger;
        _producer = new(() =>
        {
            var builder = new ProducerBuilder<TKey, TValue>(options);
            //builder.SetKeySerializer(KafkaMemoryPackSerializer<TKey>.Instance);
            //builder.SetValueSerializer(KafkaMemoryPackSerializer<TValue>.Instance);
            builder.SetKeySerializer(KafkaJsonSerializer<TKey>.Instance);
            builder.SetValueSerializer(KafkaJsonSerializer<TValue>.Instance);
            return builder.Build();
        });
    }

    public void Dispose()
    {
        if (_producer is not { IsValueCreated: true, Value: IProducer<TKey, TValue> producer })
        {
            return;
        }

        using (producer)
        using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
        {
            var token = cts.Token;
            producer.Flush(token);
            token.ThrowIfCancellationRequested();
        }
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

    public void Produce(string topic, TKey key, TValue value)
    {
        var producer = _producer.Value;

        _logger.LogInformation("Async produce to topic {topic}: (key:{key,-10},value:{value})", topic, key, value);
        producer.Produce(topic, new Message<TKey, TValue> { Key = key, Value = value });
    }

    public void HandleRevoke(IConsumer<TKey, TValue> c, TimeSpan timeout)
    {
        // All handlers (except the log handler) are executed as a
        // side-effect of, and on the same thread as the Consume or
        // Close methods. Any exception thrown in a handler (with
        // the exception of the log and error handlers) will
        // be propagated to the application via the initiating
        // call. i.e. in this example, any exceptions thrown in this
        // handler will be exposed via the Consume method in the main
        // consume loop and handled by the try/catch block there.

        //can throw in state: Ready

        var producer = _producer.Value;
        producer.SendOffsetsToTransaction(
            c.Assignment.Select(a => new TopicPartitionOffset(a, c.Position(a))),
            c.ConsumerGroupMetadata,
            timeout
        );
        producer.CommitTransaction();
        producer.BeginTransaction();
    }

    public void HandleLost()
    {
        var producer = _producer.Value;
        producer.AbortTransaction();
        producer.BeginTransaction();
    }

    public void TransactionReset(TimeSpan timeout)
    {
        var producer = _producer.Value;
        producer.InitTransactions(timeout);
        producer.BeginTransaction();
    }

    public void TransactionAbort()
    {
        var producer = _producer.Value;
        //don't pass in timeout -- see remarks
        producer.AbortTransaction();
    }

    public void TransactionCommit(IConsumer<TKey, TValue> consumer, TimeSpan timeout)
    {
        // Note: Exceptions thrown by SendOffsetsToTransaction and
        // CommitTransaction that are not marked as fatal can be
        // recovered from. However, in order to keep this example
        // short(er), the additional logic required to achieve this
        // has been omitted. This should happen only rarely, so
        // requiring a process restart in this case is not necessarily
        // a bad compromise, even in production scenarios.

        var producer = _producer.Value;
        producer.SendOffsetsToTransaction(
            // Note: committed offsets reflect the next message to consume, not last
            // message consumed. consumer.Position returns the last consumed offset
            // values + 1, as required.
            consumer.Assignment.Select(a => new TopicPartitionOffset(a, consumer.Position(a))),
            consumer.ConsumerGroupMetadata,
            timeout);
        producer.CommitTransaction();
        producer.BeginTransaction();
    }
}