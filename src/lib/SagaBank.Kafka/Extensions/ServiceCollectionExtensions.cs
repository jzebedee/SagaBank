using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace SagaBank.Kafka.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKafkaProducer<TKey, TValue>(this IServiceCollection services, Action<ProducerConfig> configureOptions)
    {
        return AddKafkaProducer<TKey, TValue>(services, Options.DefaultName, configureOptions)
            .AddSingleton<Producer<TKey, TValue>>(provider =>
            {
                var factory = provider.GetRequiredService<KafkaProducerFactory<TKey, TValue>>();
                return factory[Options.DefaultName];
            });
    }

    public static IServiceCollection AddKafkaConsumer<TKey, TValue>(this IServiceCollection services, Action<ConsumerConfig> configureOptions)
    {
        return services.Configure(configureOptions)
            .AddSingleton<Consumer<TKey, TValue>>();
    }

    public static IServiceCollection AddKafkaProducer<TKey, TValue>(this IServiceCollection services, string name, Action<ProducerConfig> configureOptions)
    {
        services.TryAddSingleton<KafkaProducerFactory<TKey, TValue>>();

        return services.Configure(name, configureOptions);
    }
}

public sealed class KafkaProducerFactory<TKey, TValue>
{
    private readonly IServiceProvider _provider;
    private readonly ConcurrentDictionary<string, Producer<TKey, TValue>> _producers = new();

    public KafkaProducerFactory(IServiceProvider provider)
    {
        _provider = provider;
    }

    public Producer<TKey, TValue> this[string name]
    {
        get
        {
            ArgumentNullException.ThrowIfNull(name);

            return _producers.GetOrAdd(name, _ =>
            {
                var logger = _provider.GetRequiredService<ILogger<Producer<TKey, TValue>>>();
                var optionsMonitor = _provider.GetRequiredService<IOptionsMonitor<ProducerConfig>>();
                return new(logger, optionsMonitor.Get(name));
            });
        }
    }
}