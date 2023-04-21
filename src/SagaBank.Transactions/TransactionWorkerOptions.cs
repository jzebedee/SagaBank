namespace SagaBank.Debits;

public class TransactionWorkerOptions
{
    public string ConsumeTopic { get; set; }
    public string ProduceTopic { get; set; }
    public TimeSpan TransactionTimeout { get; set; }
    public TimeSpan ConsumeTimeout { get; set; }
    public TimeSpan ThrottleTime { get; set; }
    public TimeSpan CommitPeriod { get; set; }
}