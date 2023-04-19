namespace SagaBank.Debits;

public class TransactionWorkerOptions
{
    public string ConsumeTopic { get; set; }
    public string ProduceTopic { get; set; }
}