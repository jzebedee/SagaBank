using Confluent.Kafka;
using SagaBank.Banking;
using SagaBank.Debits;
using SagaBank.Kafka.Extensions;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        var kafkaSection = context.Configuration.GetSection("Kafka");
        //services.AddKafkaProducer(configure => kafkaSection.GetSection(nameof(ProducerConfig)).Bind(configure));
        services.AddKafkaConsumer<Ulid, ITransactionSaga>(configure => kafkaSection.GetSection(nameof(ConsumerConfig)).Bind(configure));

        //ConsumerConfig x;
        //x.commit

        services.Configure<DebitWorkerOptions>(opt =>
        {
            opt.ConsumeTopic = kafkaSection["Topic"];
        });
        services.AddHostedService<DebitWorker>();
    })
    .Build();

await host.RunAsync();