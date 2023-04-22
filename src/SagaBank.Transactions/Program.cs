using Confluent.Kafka;
using SagaBank.Banking;
using SagaBank.Debits;
using SagaBank.Kafka.Extensions;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        var kafkaSection = context.Configuration.GetSection("Kafka");
        services.AddKafkaProducer<Ulid, ITransactionSaga>(configure => kafkaSection.GetSection(nameof(ProducerConfig)).Bind(configure));
        services.AddKafkaConsumer<Ulid, ITransactionSaga>(configure => kafkaSection.GetSection(nameof(ConsumerConfig)).Bind(configure));

        services.Configure<TransactionWorkerOptions>(opt =>
        {
            //TODO: null check / bind from config
            opt.ConsumeTopic = kafkaSection["Topic"]!;
            opt.ProduceTopic = kafkaSection["Topic"]!;
            opt.TransactionTimeout = TimeSpan.FromSeconds(30);
            opt.ConsumeTimeout = TimeSpan.FromSeconds(1);
            opt.ThrottleTime = TimeSpan.FromMilliseconds(250);
            opt.CommitPeriod = TimeSpan.FromSeconds(10);
        });

        //Tye-only
        {
            var appInstance = Environment.GetEnvironmentVariable("APP_INSTANCE");

            services.PostConfigure<ProducerConfig>(configure =>
            {
                configure.ClientId = $"{appInstance}_{configure.ClientId}";
            });
            services.PostConfigure<ConsumerConfig>(configure =>
            {
                configure.ClientId = $"{appInstance}_{configure.ClientId}";
            });
        }

        services.AddHostedService<TransactionWorker>();
    })
    .Build();

await host.RunAsync();