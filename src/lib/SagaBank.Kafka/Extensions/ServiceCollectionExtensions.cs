using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;

namespace SagaBank.Kafka.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKafkaProducer(this IServiceCollection services, Action<ProducerConfig> configureOptions)
    {
        return services.Configure(configureOptions)
            .AddSingleton<Producer>();
    }

    public static IServiceCollection AddKafkaConsumer(this IServiceCollection services, Action<ConsumerConfig> configureOptions)
    {
        return services.Configure(configureOptions)
            .AddSingleton<Consumer>();
    }
}
