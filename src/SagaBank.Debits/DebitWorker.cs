using Confluent.Kafka;
using Microsoft.Extensions.Options;
using SagaBank.Kafka;
using SagaBank.Shared.Models;

namespace SagaBank.Debits;

public class DebitWorker : BackgroundService
{
    private readonly IServiceProvider _provider;
    private readonly ILogger<DebitWorker> _logger;
    private readonly IOptions<DebitWorkerOptions> _options;
    private readonly Consumer<int, Debit> _debitConsumer;

    public DebitWorker(IServiceProvider provider, ILogger<DebitWorker> logger, IOptions<DebitWorkerOptions> options, Consumer<int, Debit> debitConsumer)
    {
        _provider = provider;
        _logger = logger;
        _options = options;
        _debitConsumer = debitConsumer;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("{worker} running at: {time}", nameof(DebitWorker), DateTimeOffset.Now);
            Consume();
        }
    }

    private void Consume()
    {
        var consumer = _debitConsumer;
        var consumeTopic = _options.Value.ConsumeTopic;
        try
        {
            switch (consumer.Consume(consumeTopic))
            {
                case Message<int, Debit> message:
                    _logger.LogInformation("Received debit {debit} on topic {topic}", message.Value, consumeTopic);
                    HandleDebit(message.Value);
                    break;
                default:
                    _logger.LogWarning("Failed to read debit message from {topic}", consumeTopic);
                    break;
            }
        }
        catch (ConsumeException ex)
        {
            _logger.LogError(ex, "Failed to consume on topic {topic}", consumeTopic);
        }
    }

    private void HandleDebit(Debit debit)
    {
        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DebitContext>();

        context.Debits.Add(debit);
        context.SaveChanges();
    }
}
