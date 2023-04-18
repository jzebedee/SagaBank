using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;

namespace SagaBank.Kafka.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKafkaProducer<TKey, TValue>(this IServiceCollection services, Action<ProducerConfig> configureOptions)
    {
        return services.Configure(configureOptions)
            .AddSingleton<Producer<TKey, TValue>>();
    }

    public static IServiceCollection AddKafkaConsumer<TKey, TValue>(this IServiceCollection services, Action<ConsumerConfig> configureOptions)
    {
        return services.Configure(configureOptions)
            .AddSingleton<Consumer<TKey, TValue>>();
    }
}
