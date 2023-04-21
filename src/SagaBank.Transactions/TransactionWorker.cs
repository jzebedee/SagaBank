using Confluent.Kafka;
using Microsoft.Extensions.Options;
using SagaBank.Banking;
using SagaBank.Kafka;

namespace SagaBank.Debits;

public class TransactionWorker : BackgroundService
{
    private readonly IServiceProvider _provider;
    private readonly IHostApplicationLifetime _hostLifetime;
    private readonly ILogger<TransactionWorker> _logger;
    private readonly IOptions<TransactionWorkerOptions> _options;

    private readonly Consumer<Ulid, ITransactionSaga> _consumer;
    private readonly Producer<Ulid, ITransactionSaga> _producer;

    public TransactionWorker(IServiceProvider provider,
                             IHostApplicationLifetime hostLifetime,
                             ILogger<TransactionWorker> logger,
                             IOptions<TransactionWorkerOptions> options,
                             Consumer<Ulid, ITransactionSaga> consumer,
                             Producer<Ulid, ITransactionSaga> producer)
    {
        _provider = provider;
        _hostLifetime = hostLifetime;
        _logger = logger;
        _options = options;

        _consumer = consumer;
        _producer = producer;

        ConfigureForExactlyOnceSemantics();
    }

    private void ConfigureForExactlyOnceSemantics()
    {
        var consumer = _consumer;
        var consumeTopic = _options.Value.ConsumeTopic;

        var producer = _producer;
        var produceTopic = _options.Value.ProduceTopic;

        var defaultTimeout = _options.Value.TransactionTimeout;

        var logger = _logger;
        consumer.GetConsumerForTopic(consumeTopic, builder =>
        {
            builder.SetPartitionsRevokedHandler((c, partitions) =>
            {
                var remaining = c.Assignment.Where(tp => !partitions.Where(x => x.TopicPartition == tp).Any());
                logger.LogDebug("Worker {worker} consumer group partitions revoked: [{revoked}], remaining: [{remaining}]",
                    nameof(TransactionWorker),
                    string.Join(',', partitions.Select(p => p.Partition.Value)),
                    string.Join(',', remaining.Select(p => p.Partition.Value))
                );

                producer.HandleRevoke(c, defaultTimeout);
            })
            .SetPartitionsLostHandler((c, partitions) =>
            {
                // Ownership of the partitions has been involuntarily lost and
                // are now likely already owned by another consumer.
                logger.LogDebug("Worker {worker} consumer group partitions lost: [{lost}]",
                    nameof(TransactionWorker),
                    string.Join(',', partitions.Select(p => p.Partition.Value))
                );

                producer.HandleLost();
            })
            .SetPartitionsAssignedHandler((c, partitions) =>
            {
                logger.LogDebug("Worker {worker} consumer group additional partitions assigned: [{assigned}], all: [{all}]",
                    nameof(TransactionWorker),
                    string.Join(',', partitions.Select(p => p.Partition.Value)),
                    string.Join(',', c.Assignment.Concat(partitions).Select(p => p.Partition.Value))
                );

                // No action is required here related to transactions - offsets
                // for the newly assigned partitions will be committed in the
                // main consume loop along with those for already assigned
                // partitions as per usual.
            });
        });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("{worker} running at {time}", nameof(TransactionWorker), DateTimeOffset.Now);

            var consumer = _consumer;
            var consumeTopic = _options.Value.ConsumeTopic;

            var producer = _producer;
            var produceTopic = _options.Value.ProduceTopic;

            producer.TransactionReset(_options.Value.TransactionTimeout);

            using var ctsConsume = new CancellationTokenSource();//CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            using var ctsCommit = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            ctsCommit.CancelAfter(_options.Value.CommitPeriod);

            while (!stoppingToken.IsCancellationRequested)
            {
                ctsConsume.CancelAfter(_options.Value.ConsumeTimeout);

                try
                {
                    ProcessTransactions(ctsConsume.Token);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "{worker} processing stopped due to error", nameof(TransactionWorker));

                    // Attempt to abort the transaction (but ignore any errors) as a measure
                    // against stalling consumption of Topic_Words.
                    producer.TransactionAbort();
                    // Note: transactions may be committed / aborted in the partitions
                    // revoked / lost handler as a side effect of the call to close.
                    consumer.Dispose();

                    break;
                }

                if (ctsCommit.IsCancellationRequested)
                {
                    producer.TransactionCommit(consumer.GetConsumerForTopic(consumeTopic), _options.Value.TransactionTimeout);
                    _logger.LogInformation("{worker} committed Kafka transaction(s)", nameof(TransactionWorker));
                    if (!ctsCommit.TryReset())
                    {
                        _logger.LogDebug("{worker} processing stopped due to commit CTS reset failure", nameof(TransactionWorker));
                        break;
                    }
                }

                if (!ctsConsume.TryReset())
                {
                    _logger.LogDebug("{worker} processing stopped due to consume CTS reset failure", nameof(TransactionWorker));
                    break;
                }
            }
        }
        finally
        {
            _logger.LogInformation("{worker} stopping at {time} and killing host", nameof(TransactionWorker), DateTimeOffset.Now);
            _hostLifetime.StopApplication();
        }
    }

    private void ProcessTransactions(CancellationToken cancellationToken)
    {
        var consumer = _consumer;
        var consumeTopic = _options.Value.ConsumeTopic;

        var producer = _producer;
        var produceTopic = _options.Value.ProduceTopic;

        try
        {
            // Do not block on Consume indefinitely to avoid the possibility of a transaction timeout.
            if (consumer.Consume(consumeTopic, cancellationToken) is not Message<Ulid, ITransactionSaga> message)
            {
                _logger.LogWarning("Failed to read transaction saga message from {topic}", consumeTopic);
                return;
            }

            ITransactionSaga? reply = message.Value switch
            {
                TransactionStarting txStart => HandleTransactionStart(txStart),
                _ => null
            };

            if (reply is null)
            {
                _logger.LogWarning("Unknown transaction saga type {tx} from {topic}", message.Value, consumeTopic);
                return;
            }

            _logger.LogInformation("Replied to transaction saga with {reply} on topic {topic}", reply, produceTopic);
        }
        catch (ConsumeException ex)
        {
            _logger.LogError(ex, "Failed to consume on topic {topic}", consumeTopic);
        }

        return;

        //async ValueTask<bool> TryProduceAsync(Ulid key, ITransactionSaga value, CancellationToken cancellationToken)
        //{
        //    while (true)
        //    {
        //        try
        //        {
        //            producer.Produce(produceTopic, key, value);
        //            return true;
        //        }
        //        catch (KafkaException e) when (e is { Error.Code: ErrorCode.Local_QueueFull })
        //        {
        //            // An immediate failure of the produce call is most often caused by the
        //            // local message queue being full, and appropriate response to that is
        //            // to wait a bit and retry.
        //            await Task.Delay(_options.Value.ThrottleTime, cancellationToken);
        //            if (cancellationToken.IsCancellationRequested)
        //            {
        //                return false;
        //            }
        //            continue;
        //        }
        //    }
        //}

        static Dictionary<string, string[]> Problems(string reason, params string[] errors)
            => new() { { reason, errors } };

        ITransactionSaga HandleTransactionStart(TransactionStarting tx)
        {
            _logger.LogInformation("Transaction starting {tx} on topic {topic}", tx, consumeTopic);

            ITransactionSaga reply = tx switch
            {
                var t when t.DebitAccountId == t.CreditAccountId
                    => new TransactionStartFailed(tx.TransactionId, Problems("same-accounts", "Debit and credit accounts can not be the same")),
                var t when t.Amount <= 0
                    => new TransactionStartFailed(tx.TransactionId, Problems("bad-amount", "Amount must be greater than zero")),
                var t => new TransactionUpdateBalanceAvailable(tx.TransactionId, -t.Amount, tx.DebitAccountId)
            };
            producer.Produce(produceTopic, tx.TransactionId, reply);

            return reply;
        }
    }
}
