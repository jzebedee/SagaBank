using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SagaBank.Kafka;

public class Consumer
{
    private readonly IOptions<ConsumerConfig> _options;
    private readonly ILogger _logger;

    public Consumer(ILogger<Consumer> logger, IOptions<ConsumerConfig> options)
    {
        _logger = logger;
        _options = options;
    }

    public void ConsumeDummyData(string topic)
    {
        using var consumer = new ConsumerBuilder<string, string>(_options.Value).Build();
        consumer.Subscribe(topic);
        try
        {
            //while (true)
            {
                if (consumer.Consume(TimeSpan.FromSeconds(10)) is ConsumeResult<string, string> cr)
                {
                    _logger.LogInformation("Consumed event from topic {topic} with key {key,-10} and value {value}", topic, cr.Message.Key, cr.Message.Value);
                    return;
                }

                _logger.LogWarning("Failed to consume any event");
            }
        }
        finally
        {
            consumer.Close();
        }
    }
}