using Confluent.Kafka;
using Microsoft.Extensions.Options;
using SagaBank.Banking;
using SagaBank.Kafka;
using SagaBank.Shared.Models;

namespace SagaBank.Debits;

public class TransactionWorker : BackgroundService
{
    private readonly IServiceProvider _provider;
    private readonly ILogger<TransactionWorker> _logger;
    private readonly IOptions<TransactionWorkerOptions> _options;
    private readonly Consumer<Ulid, ITransactionSaga> _debitConsumer;

    public TransactionWorker(IServiceProvider provider, ILogger<TransactionWorker> logger, IOptions<TransactionWorkerOptions> options, Consumer<Ulid, ITransactionSaga> debitConsumer)
    {
        _provider = provider;
        _logger = logger;
        _options = options;
        _debitConsumer = debitConsumer;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("{worker} running at: {time}", nameof(TransactionWorker), DateTimeOffset.Now);
        while (!stoppingToken.IsCancellationRequested)
        {
            Consume();
        }
        _logger.LogInformation("{worker} stopping at: {time}", nameof(TransactionWorker), DateTimeOffset.Now);
    }

    private void Consume()
    {
        var consumer = _debitConsumer;
        var consumeTopic = _options.Value.ConsumeTopic;
        try
        {
            if(consumer.Consume(consumeTopic) is not Message<Ulid, ITransactionSaga> message)
            {
                _logger.LogWarning("Failed to read transaction saga message from {topic}", consumeTopic);
                return;
            }

            switch (message.Value)
            {
                case TransactionStarting txStart:
                    _logger.LogInformation("Transaction starting {tx} on topic {topic}", txStart, consumeTopic);
                    break;
                default:
                    _logger.LogWarning("Unknown transaction saga type {tx} from {topic}", message.Value, consumeTopic);
                    break;
            }

            consumer.Commit(consumeTopic);
        }
        catch (ConsumeException ex)
        {
            _logger.LogError(ex, "Failed to consume on topic {topic}", consumeTopic);
        }
    }
}
