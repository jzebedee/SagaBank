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

    private readonly Consumer<TransactionKey, ITransactionSaga> _consumer;
    private readonly Producer<TransactionKey, ITransactionSaga> _producer;

    public TransactionWorker(IServiceProvider provider,
                             IHostApplicationLifetime hostLifetime,
                             ILogger<TransactionWorker> logger,
                             IOptions<TransactionWorkerOptions> options,
                             Consumer<TransactionKey, ITransactionSaga> consumer,
                             Producer<TransactionKey, ITransactionSaga> producer)
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
                logger.LogDebug("{worker} consumer group partitions revoked: [{revoked}], remaining: [{remaining}]",
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
                logger.LogDebug("{worker} consumer group partitions lost: [{lost}]",
                    nameof(TransactionWorker),
                    string.Join(',', partitions.Select(p => p.Partition.Value))
                );

                producer.HandleLost();
            })
            .SetPartitionsAssignedHandler((c, partitions) =>
            {
                logger.LogDebug("{worker} consumer group additional partitions assigned: [{assigned}], all: [{all}]",
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
            var consumer = _consumer;
            var consumeTopic = _options.Value.ConsumeTopic;

            var producer = _producer;
            var produceTopic = _options.Value.ProduceTopic;

            _logger.LogInformation("{worker} running at {time}",
                nameof(TransactionWorker),
                DateTimeOffset.Now
            );

            producer.TransactionReset(_options.Value.TransactionTimeout);

            DateTimeOffset nextCommit = DateTimeOffset.Now + _options.Value.CommitPeriod;
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    ProcessTransactions();

                    if (nextCommit <= DateTimeOffset.Now)
                    {
                        _logger.LogInformation("{worker} scheduled to commit Kafka transaction(s)", nameof(TransactionWorker));
                        producer.TransactionCommit(consumer.GetConsumerForTopic(consumeTopic), _options.Value.TransactionTimeout);
                        nextCommit = DateTimeOffset.Now + _options.Value.CommitPeriod;
                    }
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
            }
        }
        finally
        {
            _logger.LogInformation("{worker} stopping at {time} and killing host", nameof(TransactionWorker), DateTimeOffset.Now);
            _hostLifetime.StopApplication();
        }
    }

    private void ProcessTransactions()
    {
        var consumer = _consumer;
        var consumeTopic = _options.Value.ConsumeTopic;

        var producer = _producer;
        var produceTopic = _options.Value.ProduceTopic;

        try
        {
            // Do not block on Consume indefinitely to avoid the possibility of a transaction timeout.
            if (consumer.Consume(consumeTopic, _options.Value.ConsumeTimeout) is not Message<TransactionKey, ITransactionSaga> message)
            {
                //_logger.LogWarning("Failed to read transaction saga message from {topic} within {timeout}", consumeTopic, _options.Value.ConsumeTimeout);
                return;
            }

            var tx = message.Value;
            _logger.LogInformation("Transaction {tx} on topic {topic}", tx, consumeTopic);

            ITransactionSaga? reply = message.Value switch
            {
                TransactionStarting txStart => HandleTransactionStart(txStart),
                TransactionUpdateBalanceAvailableSuccess txBalASuccess => HandleUpdateBalanceAvailableSuccess(txBalASuccess),
                _ => null
            };

            if (reply is null)
            {
                _logger.LogWarning("Ignoring transaction saga type {tx} from {topic}", message.Value, consumeTopic);
                //producer.TransactionReset(_options.Value.TransactionTimeout);
                return;
            }

            _logger.LogInformation("Replied to transaction saga with {reply} on topic {topic}", reply, produceTopic);
        }
        catch (ConsumeException ex)
        {
            _logger.LogError(ex, "Failed to consume on topic {topic}", consumeTopic);
        } 

        return;

        //async ValueTask<bool> TryProduceAsync(TransactionKey key, ITransactionSaga value, CancellationToken cancellationToken)
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
            ITransactionSaga reply = tx switch
            {
                var t when t.Request.DebitAccountId == t.Request.CreditAccountId
                    => new TransactionStartFailed(tx.Request, Problems("same-accounts", "Debit and credit accounts can not be the same")),
                var t when t.Request.Amount <= 0
                    => new TransactionStartFailed(tx.Request, Problems("bad-amount", "Amount must be greater than zero")),
                var t => new TransactionUpdateBalanceAvailable(tx.Request, -t.Request.Amount, tx.Request.DebitAccountId)
            };
            producer.Produce(produceTopic,
                new(DebitAccountId: tx.Request.DebitAccountId/*, CreditAccountId: tx.CreditAccountId*/),
                reply);

            return reply;
        }

        ITransactionSaga HandleUpdateBalanceAvailableSuccess(TransactionUpdateBalanceAvailableSuccess tx)
        {
            ITransactionSaga reply = tx switch
            {
                var t when t.Request.DebitAccountId == t.AccountId
                    => new TransactionUpdateCredit(tx.Request, tx.Request.Amount, tx.Request.CreditAccountId),
                _ => throw new InvalidOperationException()
                //var t when t.Request.Amount <= 0
                //    => new TransactionStartFailed(tx.Request, Problems("bad-amount", "Amount must be greater than zero")),
                //var t => new TransactionUpdateBalanceAvailable(tx.Request, -t.Request.Amount, tx.Request.DebitAccountId)
            };
            producer.Produce(produceTopic,
                new(DebitAccountId: tx.Request.DebitAccountId/*, CreditAccountId: tx.CreditAccountId*/),
                reply);

            return reply;
        }
    }
}
