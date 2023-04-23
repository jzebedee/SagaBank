using Confluent.Kafka;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SagaBank.Backend;
using SagaBank.Backend.Models;
using SagaBank.Banking;
using SagaBank.Kafka;
using SagaBank.Kafka.Extensions;

var builder = WebApplication.CreateBuilder(args);

var dbConnectionString = builder.Configuration.GetConnectionString(nameof(BankContext));
builder.Services.AddDbContext<BankContext>(options => options.UseSqlite(dbConnectionString));

var kafkaSection = builder.Configuration.GetSection("Kafka");
builder.Services.AddKafkaProducer<TransactionKey, ITransactionSaga>(configure => kafkaSection.GetSection(nameof(ProducerConfig)).Bind(configure));
//builder.Services.AddKafkaProducer<string, string>(configure => kafkaSection.GetSection(nameof(ProducerConfig)).Bind(configure));
//builder.Services.AddKafkaProducer<int, Credit>(configure => kafkaSection.GetSection(nameof(ProducerConfig)).Bind(configure));
//builder.Services.AddKafkaConsumer(configure => kafkaSection.GetSection(nameof(ConsumerConfig)).Bind(configure));

var app = builder.Build();

//FIXME: only for demo, don't use Migrate() in prod
{
    using var scope = app.Services.CreateScope();
    var bank = scope.ServiceProvider.GetRequiredService<BankContext>();
    bank.Database.Migrate();
}

app.MapGet("/", () => $"Hello World! It's {DateTimeOffset.Now}");
//app.MapGet("/produce", ([FromServices] Producer producer) => producer.ProduceDummyData(kafkaSection["Topic"]));
//app.MapGet("/consume", ([FromServices] Consumer consumer) => consumer.ConsumeDummyData(kafkaSection["Topic"]));

//app.MapGet("/audit/{id}", (int id) =>
//{

//});
var topicSection = kafkaSection.GetSection("Topics");
var txTopic = topicSection["Transaction"];
ArgumentException.ThrowIfNullOrEmpty(txTopic);
app.MapGet("/transactions/{id}", (Ulid id) =>
    {
        return Results.NoContent();
    })
    .WithName("GetTransactions");
app.MapPost("/transactions",
    async ([FromBody] TransactionRequest tx, BankContext bank, Producer<TransactionKey, ITransactionSaga> txProducer) =>
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
            new(DebitAccountId: tx.DebitAccountId, CreditAccountId: tx.CreditAccountId),
            new TransactionStarting(txid, tx.Amount, tx.DebitAccountId, tx.CreditAccountId));
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
