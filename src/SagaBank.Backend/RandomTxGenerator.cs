using Microsoft.Extensions.Options;
using SagaBank.Banking;
using SagaBank.Kafka;

public class RandomTxGenerator : BackgroundService
{
    //private readonly IServiceProvider _provider;
    private readonly Producer<TransactionKey, ITransactionSaga> _producer;
    private readonly RandomTxGeneratorOptions _options;

    private readonly Random _rand = new();

    public RandomTxGenerator(/*IServiceProvider provider, */Producer<TransactionKey, ITransactionSaga> producer, IOptions<RandomTxGeneratorOptions> options)
    {
        //_provider = provider;
        _producer = producer;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while(!stoppingToken.IsCancellationRequested)
        {
            var accountDebit = _rand.Next(1, 10_001);
            var accountCredit = _rand.Next(1, 10_001);
            var amount = (decimal)(_rand.NextDouble() * 1_000);

            //using var scope = _provider.CreateScope();
            //using var db = scope.ServiceProvider.GetRequiredService<BankContext>();

            var txid = Ulid.NewUlid();

            _producer.Produce(_options.ProduceTopic,
                new(DebitAccountId: accountDebit/*, CreditAccountId: account*/),
                new TransactionStarting(new(txid, amount, accountDebit, accountCredit)));

            await Task.Delay(_options.ProduceDelay, stoppingToken);
        }
    }
}
