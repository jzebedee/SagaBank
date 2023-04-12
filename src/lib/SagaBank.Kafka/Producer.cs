using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SagaBank.Kafka;

public sealed class Producer : IDisposable
{
    private readonly IOptions<ProducerConfig> _options;
    private readonly ILogger _logger;

    private readonly Lazy<IProducer<string, string>> _producer;

    public Producer(ILogger<Producer> logger, IOptions<ProducerConfig> options)
    {
        _logger = logger;
        _options = options;
        _producer = new(() => new ProducerBuilder<string, string>(_options.Value).Build());
    }

    public void Dispose()
    {
        if(_producer is { IsValueCreated: true, Value: IProducer<string, string> producer })
        {
            producer.Dispose();
        }
    }

    public void ProduceDummyData(string topic)
    {
        string[] users = { "eabara", "jsmith", "sgarcia", "jbernard", "htanaka", "awalther" };
        string[] items = { "book", "alarm clock", "t-shirts", "gift card", "batteries" };

        var producer = _producer.Value;
        var numProduced = 0;
        var rnd = Random.Shared;

        const int numMessages = 10;
        for (int i = 0; i < numMessages; ++i)
        {
            var user = users[rnd.Next(users.Length)];
            var item = items[rnd.Next(items.Length)];

            producer.Produce(topic, new Message<string, string> { Key = user, Value = item },
                (deliveryReport) =>
                {
                    if (deliveryReport is { Error.Code: ErrorCode.NoError })
                    {
                        numProduced++;
                        _logger.LogInformation("Produced event to topic {topic}: key = {user,-10} value = {item}", topic, user, item);
                        return;
                    }

                    _logger.LogWarning("Failed to deliver message: {reason}", deliveryReport.Error.Reason);
                });
        }

        producer.Flush(TimeSpan.FromSeconds(10));
        _logger.LogInformation("{numProduced} messages were produced to topic {topic}", numProduced, topic);
    }
}