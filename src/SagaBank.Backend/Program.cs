using Confluent.Kafka;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SagaBank.Backend;
using SagaBank.Backend.Models;
using SagaBank.Kafka;
using SagaBank.Kafka.Extensions;
using SagaBank.Shared.Models;

var builder = WebApplication.CreateBuilder(args);

var dbConnectionString = builder.Configuration.GetConnectionString(nameof(BankContext));
builder.Services.AddDbContext<BankContext>(options => options.UseSqlite(dbConnectionString));

var kafkaSection = builder.Configuration.GetSection("Kafka");
builder.Services.AddKafkaProducer<int, Debit>(configure => kafkaSection.GetSection(nameof(ProducerConfig)).Bind(configure));
builder.Services.AddKafkaProducer<int, Credit>(configure => kafkaSection.GetSection(nameof(ProducerConfig)).Bind(configure));
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
var debitTopic = topicSection["Debit"];
var creditTopic = topicSection["Credit"];
ArgumentException.ThrowIfNullOrEmpty(debitTopic);
ArgumentException.ThrowIfNullOrEmpty(creditTopic);
app.MapGet("/transactions/{id}", (Ulid id) =>
    {
        return Results.NoContent();
    })
    .WithName("GetTransactions");
app.MapPost("/transactions",
    ([FromBody] Transaction tx, BankContext bank, Producer<int, Debit> debitProducer, Producer<int, Credit> creditProducer) =>
    {
        if (tx.DebitAccountId == tx.CreditAccountId)
        {
            return Results.BadRequest("Debit and credit accounts can not be the same");
        }

        if (tx.Amount <= 0)
        {
            return Results.BadRequest("Amount must be greater than zero");
        }

        if (!bank.Accounts.Any(a => a.AccountId == tx.DebitAccountId))
        {
            return Results.BadRequest("Debit account does not exist");
        }

        if (!bank.Accounts.Any(a => a.AccountId == tx.CreditAccountId))
        {
            return Results.BadRequest("Credit account does not exist");
        }

        var txid = Ulid.NewUlid();
        var timestamp = DateTimeOffset.Now;

        var debit = new Debit
        {
            TransactionId = txid,
            DebitAccountId = tx.DebitAccountId,
            Amount = tx.Amount,
            Timestamp = timestamp
        };
        debitProducer.Produce(debitTopic, tx.DebitAccountId, debit);

        var credit = new Credit
        {
            TransactionId = txid,
            CreditAccountId = tx.CreditAccountId,
            Amount = tx.Amount,
            Timestamp = timestamp
        };
        creditProducer.Produce(creditTopic, tx.CreditAccountId, credit);

        return Results.CreatedAtRoute("GetTransactions", new { id = txid }/*, new { debit, credit }*/);
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
