using SagaBank.Shared;
using System.ComponentModel.DataAnnotations;

namespace SagaBank.Backend.Models;

public class Transaction
{
    [Required]
    [Range(0, int.MaxValue)]
    public decimal Amount { get; set; }
    [Required]
    [Range(0, int.MaxValue)]
    [NotCompare(nameof(CreditAccountId), ErrorMessage = "Debit and credit accounts can not be the same")]
    public int DebitAccountId { get; set; }
    [Required]
    [Range(0, int.MaxValue)]
    public int CreditAccountId { get; set; }
}