namespace SagaBank.Backend.Models;

public class ExternalAccount : Account
{
    public string BankName { get; set; }
    public string ExternalAccountId { get; set; }
}

