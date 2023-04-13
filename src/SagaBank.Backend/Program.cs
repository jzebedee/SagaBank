using Confluent.Kafka;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SagaBank.Backend;
using SagaBank.Backend.Models;
using SagaBank.Kafka;
using SagaBank.Kafka.Extensions;

var builder = WebApplication.CreateBuilder(args);

var dbConnectionString = builder.Configuration.GetConnectionString(nameof(AccountContext));
builder.Services.AddDbContext<AccountContext>(options => options.UseSqlite(dbConnectionString));

var kafkaSection = builder.Configuration.GetSection("Kafka");
builder.Services.AddKafkaProducer(configure => kafkaSection.GetSection(nameof(ProducerConfig)).Bind(configure));
builder.Services.AddKafkaConsumer(configure => kafkaSection.GetSection(nameof(ConsumerConfig)).Bind(configure));

var app = builder.Build();

//FIXME: only for demo, don't use Migrate() in prod
{
    using var scope = app.Services.CreateScope();
    var bank = scope.ServiceProvider.GetRequiredService<AccountContext>();
    bank.Database.Migrate();
}

app.MapGet("/", () => $"Hello World! It's {DateTimeOffset.Now}");
app.MapGet("/produce", ([FromServices] Producer producer) => producer.ProduceDummyData(kafkaSection["Topic"]));
app.MapGet("/consume", ([FromServices] Consumer consumer) => consumer.ConsumeDummyData(kafkaSection["Topic"]));

//app.MapGet("/audit/{id}", (int id) =>
//{

//});
app.MapPost("/transactions",
    ([FromBody] Transaction tx) =>
    {
        if(tx.DebitAccountId == tx.CreditAccountId)
        {
            return Results.BadRequest("Debit and credit accounts can not be the same");
        }

        if(tx.Amount <= 0)
        {
            return Results.BadRequest("Amount must be greater than zero");
        }

        return Results.Ok(tx);
    });
app.MapGet("/accounts/{id}",
    (int id, [FromServices] AccountContext bank) =>
    bank.Accounts.SingleOrDefault(a => a.AccountId == id) switch
    {
        Account account => Results.Ok(account),
        null => Results.NotFound()
    });
app.MapPost("/accounts",
    (Account account, [FromServices] AccountContext bank, [FromServices] ILogger<Program> logger) =>
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
