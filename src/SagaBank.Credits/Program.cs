using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using SagaBank.Credits;
using SagaBank.Kafka.Extensions;
using SagaBank.Shared.Models;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        var dbConnectionString = context.Configuration.GetConnectionString(nameof(CreditContext));
        services.AddDbContext<CreditContext>(options => options.UseSqlite(dbConnectionString));

        var kafkaSection = context.Configuration.GetSection("Kafka");
        //services.AddKafkaProducer(configure => kafkaSection.GetSection(nameof(ProducerConfig)).Bind(configure));
        services.AddKafkaConsumer<int, Credit>(configure => kafkaSection.GetSection(nameof(ConsumerConfig)).Bind(configure));

        services.Configure<CreditWorkerOptions>(opt =>
        {
            opt.ConsumeTopic = kafkaSection["Topic"];
        });
        services.AddHostedService<CreditWorker>();
    })
    .Build();

//FIXME: only for demo, don't use Migrate() in prod
{
    using var scope = host.Services.CreateScope();
    var bank = scope.ServiceProvider.GetRequiredService<CreditContext>();
    bank.Database.Migrate();
}

await host.RunAsync();