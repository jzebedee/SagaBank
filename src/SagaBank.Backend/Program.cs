using Confluent.Kafka;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SagaBank.Backend;
using SagaBank.Backend.Models;
using SagaBank.Backend.Workers;
using SagaBank.Banking;
using SagaBank.Kafka;
using SagaBank.Kafka.Extensions;
using SagaBank.Shared.Contexts;

var builder = WebApplication.CreateBuilder(args);

var dbConnectionString = builder.Configuration.GetConnectionString(nameof(BankContext));
builder.Services.AddDbContext<BankContext>(
    options => options
        .UseSqlite(dbConnectionString)
        .EnableSensitiveDataLogging());
builder.Services.AddDbContext<MessageBoxContext>(
    (provider, options) =>
    {
        var bank = provider.GetRequiredService<BankContext>();
        options
            .UseSqlite(bank.Database.GetDbConnection(), b => b.MigrationsAssembly("SagaBank.Backend"))
            .EnableSensitiveDataLogging();
    });

var kafkaSection = builder.Configuration.GetSection("Kafka");
var topicSection = kafkaSection.GetSection("Topics");
var txTopic = topicSection["Transaction"];
ArgumentException.ThrowIfNullOrEmpty(txTopic);

const string TxProducerName = "TransactionalProducer";

builder.Services.AddKafkaProducer<TransactionKey, ITransactionSaga>(configure => kafkaSection.GetSection(nameof(ProducerConfig)).Bind(configure));
builder.Services.AddKafkaProducer<TransactionKey, ITransactionSaga>(TxProducerName, configure => kafkaSection.GetSection($"{TxProducerName}Config").Bind(configure));
builder.Services.AddKafkaConsumer<TransactionKey, ITransactionSaga>(configure => kafkaSection.GetSection(nameof(ConsumerConfig)).Bind(configure));
//builder.Services.AddKafkaProducer<string, string>(configure => kafkaSection.GetSection(nameof(ProducerConfig)).Bind(configure));
//builder.Services.AddKafkaProducer<int, Credit>(configure => kafkaSection.GetSection(nameof(ProducerConfig)).Bind(configure));
//builder.Services.AddKafkaConsumer(configure => kafkaSection.GetSection(nameof(ConsumerConfig)).Bind(configure));

builder.Services.Configure<RandomLoanGeneratorOptions>(configure =>
{
    configure.ProviderAccountId = 0;
    configure.ProduceTopic = txTopic;
    configure.ProduceDelay = TimeSpan.FromSeconds(5); //TimeSpan.FromMilliseconds(100);
});
builder.Services.AddHostedService<RandomLoanGenerator>();

//Tye-only
{
    var appInstance = Environment.GetEnvironmentVariable("APP_INSTANCE");

    builder.Services.PostConfigureAll<ProducerConfig>(configure =>
    {
        if (configure.ClientId is not null)
        {
            configure.ClientId = $"{appInstance}_{configure.ClientId}";
        }
        if (configure.TransactionalId is not null)
        {
            configure.TransactionalId = $"{appInstance}_{configure.TransactionalId}";
        }
    });
    builder.Services.PostConfigureAll<ConsumerConfig>(configure =>
    {
        if (configure.ClientId is not null)
        {
            configure.ClientId = $"{appInstance}_{configure.ClientId}";
        }
    });
}

builder.Services.Configure<BackendTransactionWorkerOptions>(opt =>
{
    //TODO: null check / bind from config
    opt.ProducerName = TxProducerName;
    opt.ConsumeTopic = txTopic;
    opt.ProduceTopic = txTopic;
    opt.TransactionTimeout = TimeSpan.FromSeconds(15);
    opt.ConsumeTimeout = TimeSpan.FromMilliseconds(500);
    opt.ThrottleTime = TimeSpan.FromMilliseconds(125);
    opt.CommitPeriod = TimeSpan.FromSeconds(5);
});
builder.Services.AddHostedService<BackendTransactionWorker>();

var app = builder.Build();

//FIXME: only for demo, don't use Migrate() in prod
{
    using var scope = app.Services.CreateScope();
    //MessageBoxContext
    using var bank = scope.ServiceProvider.GetRequiredService<BankContext>();
    bank.Database.Migrate();

    using var messageBox = scope.ServiceProvider.GetRequiredService<MessageBoxContext>();
    messageBox.Database.Migrate();

    //bank.Accounts.ExecuteDelete();
    if (!bank.Accounts.Any())
    {
        //seed database
        bank.Accounts.AddRange(GenerateSeedAccounts());
        bank.SaveChanges();
    }
}

app.MapGet("/", () => $"Hello World! It's {DateTimeOffset.Now}");
//app.MapGet("/produce", ([FromServices] Producer producer) => producer.ProduceDummyData(kafkaSection["Topic"]));
//app.MapGet("/consume", ([FromServices] Consumer consumer) => consumer.ConsumeDummyData(kafkaSection["Topic"]));

//app.MapGet("/audit/{id}", (int id) =>
//{

//});
app.MapGet("/transactions/{id}", (Ulid id) =>
    {
        return Results.NoContent();
    })
    .WithName("GetTransactions");
app.MapPost("/transactions",
    async ([FromBody] BackendTransactionRequest tx, BankContext bank, Producer<TransactionKey, ITransactionSaga> txProducer) =>
    {
        //if (tx.DebitAccountId == tx.CreditAccountId)
        //{
        //    return Results.BadRequest("Debit and credit accounts can not be the same");
        //}

        //if (tx.Amount <= 0)
        //{
        //    return Results.BadRequest("Amount must be greater than zero");
        //}

        //if (!bank.Accounts.Any(a => a.AccountId == tx.DebitAccountId))
        //{
        //    return Results.BadRequest("Debit account does not exist");
        //}

        //if (!bank.Accounts.Any(a => a.AccountId == tx.CreditAccountId))
        //{
        //    return Results.BadRequest("Credit account does not exist");
        //}

        var txid = Ulid.NewUlid();
        var result = await txProducer.ProduceAsync(txTopic,
            new(DebitAccountId: tx.DebitAccountId/*, CreditAccountId: tx.CreditAccountId*/),
            new TransactionStarting(new(txid, tx.Amount, tx.DebitAccountId, tx.CreditAccountId)));

        return result.Status switch
        {
            PersistenceStatus.Persisted => Results.CreatedAtRoute("GetTransactions", new { id = txid }/*, new { debit, credit }*/),
            PersistenceStatus.PossiblyPersisted => Results.AcceptedAtRoute("GetTransactions", new { id = txid }),
            _ => Results.UnprocessableEntity()
        };
    });
app.MapGet("/accounts/{id}",
    (int id, [FromServices] BankContext bank) =>
    bank.Accounts.SingleOrDefault(a => a.AccountId == id) switch
    {
        Account account => Results.Ok(account),
        null => Results.NotFound()
    });
app.MapPost("/accounts",
    (Account account, [FromServices] BankContext bank, [FromServices] ILogger<Program> logger) =>
    {
        try
        {
            bank.Accounts.Add(account);
            bank.SaveChanges();

            return Results.Created($"/accounts/{account.AccountId}", account);
        }
        catch (DbUpdateException outerEx) when (outerEx.InnerException is SqliteException ex and { SqliteErrorCode: SQLitePCL.raw.SQLITE_CONSTRAINT })
        {
            logger.LogError(outerEx, "New account conflict");
            return Results.Conflict(account);
        }
    });

//app.MapGet("/accounts/{id}",
//    (int id, [FromServices] BankContext bank) =>
//    {
//        return bank.Accounts.Single(a => a.AccountId == id);
//    });

app.Run();

static IEnumerable<InternalAccount> GenerateSeedAccounts()
{
    yield return new InternalAccount
    {
        AccountId = 0,
        Balance = 1_000_000,
        BalanceAvailable = 1_000_000,
        Frozen = false
    };

    for (int i = 1; i <= 10_000; i++)
    {
        yield return new InternalAccount
        {
            AccountId = i,
            Balance = 0,
            BalanceAvailable = 0,
            Frozen = i % 500 == 0
        };
    }
}