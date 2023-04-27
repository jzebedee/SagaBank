using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using SagaBank.Backend.Models;
using SagaBank.Banking;
using SagaBank.Kafka;
using SagaBank.Kafka.Extensions;
using SagaBank.Shared.Contexts;
using System.Buffers;
using System.Security.Principal;

namespace SagaBank.Backend.Workers;

public class BackendTransactionWorker : BackgroundService
{
    private readonly IServiceProvider _provider;
    private readonly IHostApplicationLifetime _hostLifetime;
    private readonly ILogger<BackendTransactionWorker> _logger;
    private readonly IOptions<BackendTransactionWorkerOptions> _options;

    private readonly Consumer<TransactionKey, ITransactionSaga> _consumer;
    private readonly Producer<TransactionKey, ITransactionSaga> _producer;

    public BackendTransactionWorker(IServiceProvider provider,
                             IHostApplicationLifetime hostLifetime,
                             ILogger<BackendTransactionWorker> logger,
                             IOptions<BackendTransactionWorkerOptions> options,
                             Consumer<TransactionKey, ITransactionSaga> consumer,
                             KafkaProducerFactory<TransactionKey, ITransactionSaga> producerFactory)
    {
        _provider = provider;
        _hostLifetime = hostLifetime;
        _logger = logger;
        _options = options;

        _consumer = consumer;
        _producer = producerFactory[options.Value.ProducerName];

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
                    nameof(BackendTransactionWorker),
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
                    nameof(BackendTransactionWorker),
                    string.Join(',', partitions.Select(p => p.Partition.Value))
                );

                producer.HandleLost();
            })
            .SetPartitionsAssignedHandler((c, partitions) =>
            {
                logger.LogDebug("{worker} consumer group additional partitions assigned: [{assigned}], all: [{all}]",
                    nameof(BackendTransactionWorker),
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
                nameof(BackendTransactionWorker),
                DateTimeOffset.Now
            );

            producer.TransactionReset(_options.Value.TransactionTimeout);

            DateTimeOffset nextCommit = DateTimeOffset.Now + _options.Value.CommitPeriod;
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    ConsumeKafkaTransactions();

                    if (nextCommit <= DateTimeOffset.Now)
                    {
                        _logger.LogInformation("{worker} scheduled to commit Kafka transaction(s)", nameof(BackendTransactionWorker));
                        producer.TransactionCommit(consumer.GetConsumerForTopic(consumeTopic), _options.Value.TransactionTimeout);
                        nextCommit = DateTimeOffset.Now + _options.Value.CommitPeriod;
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "{worker} processing stopped due to error", nameof(BackendTransactionWorker));

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
            _logger.LogInformation("{worker} stopping at {time} and killing host", nameof(BackendTransactionWorker), DateTimeOffset.Now);
            _hostLifetime.StopApplication();
        }
    }

    private void ConsumeKafkaTransactions()
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

            ITransactionSaga? reply = message.Value switch
            {
                TransactionUpdateBalanceAvailable updateBalA => HandleUpdateBalA(updateBalA),
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

        ITransactionSaga? HandleUpdateBalA(TransactionUpdateBalanceAvailable tx)
        {
            using var scope = _provider.CreateScope();

            using var bank = scope.ServiceProvider.GetRequiredService<BankContext>();
            using var messageBox = scope.ServiceProvider.GetRequiredService<MessageBoxContext>();

            var incoming = new InboxMessage
            {
                Id = tx.Request.TransactionId,
                Type = typeof(TransactionUpdateBalanceAvailable).Name
            };

            ITransactionSaga? reply = default;
            messageBox.Process(incoming, dbTx =>
            {
                bank.Database.UseTransaction(dbTx.GetDbTransaction());
                reply = bank.InternalAccounts.SingleOrDefault(ia => ia.AccountId == tx.AccountId) switch
                {
                    InternalAccount ia when (ia.BalanceAvailable += tx.Amount) >= 0 => new TransactionUpdateBalanceAvailableSuccess(tx.Request, tx.Amount, tx.AccountId),
                    InternalAccount ia => new TransactionUpdateBalanceAvailableFailed(tx.Request, Problems("balance-insufficient", "There were insufficient funds available to debit")),
                    null => new TransactionUpdateBalanceAvailableFailed(tx.Request, Problems("bad-account", "Account was not found"))
                };
                bank.SaveChanges();

                //TODO: memorypack in prod
                byte[] payload;
                {
                    using var ms = new MemoryStream();
                    System.Text.Json.JsonSerializer.Serialize(ms, reply);
                    payload = ms.ToArray();
                }

                return new OutboxMessage
                {
                    Id = tx.Request.TransactionId,
                    Type = reply.GetType().Name,
                    Payload = payload
                };
            });

            return reply;
        }
    }
}
