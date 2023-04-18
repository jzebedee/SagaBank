using Confluent.Kafka;
using Microsoft.Extensions.Options;
using SagaBank.Kafka;
using SagaBank.Shared.Models;

namespace SagaBank.Credits;

public class CreditWorker : BackgroundService
{
    private readonly IServiceProvider _provider;
    private readonly ILogger<CreditWorker> _logger;
    private readonly IOptions<CreditWorkerOptions> _options;
    private readonly Consumer<int, Credit> _creditConsumer;

    public CreditWorker(IServiceProvider provider, ILogger<CreditWorker> logger, IOptions<CreditWorkerOptions> options, Consumer<int, Credit> CreditConsumer)
    {
        _provider = provider;
        _logger = logger;
        _options = options;
        _creditConsumer = CreditConsumer;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("{worker} running at: {time}", nameof(CreditWorker), DateTimeOffset.Now);
            Consume();
            await Task.Delay(500, stoppingToken);
        }
    }

    private void Consume()
    {
        var consumer = _creditConsumer;
        var consumeTopic = _options.Value.ConsumeTopic;
        try
        {
            switch (consumer.Consume(consumeTopic))
            {
                case Message<int, Credit> message:
                    _logger.LogInformation("Received credit {credit} on topic {topic}", message.Value, consumeTopic);
                    HandleCredit(message.Value);
                    break;
                default:
                    _logger.LogWarning("Failed to read credit message from {topic}", consumeTopic);
                    break;
            }
        }
        catch (ConsumeException ex)
        {
            _logger.LogError(ex, "Failed to consume on topic {topic}", consumeTopic);
        }
    }

    private void HandleCredit(Credit credit)
    {
        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CreditContext>();

        context.Credits.Add(credit);
        context.SaveChanges();
    }
}
