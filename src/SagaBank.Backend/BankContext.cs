using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace SagaBank.Backend;

public class BankContext : DbContext
{
    public DbSet<Account> Accounts { get; set; }
    public DbSet<Transaction> Transactions { get; set; }

    public BankContext(DbContextOptions<BankContext> options) : base(options) { }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.AddInterceptors(new SqliteWalDbConnectionInterceptor());
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>()
            .HasMany(e => e.Debits)
            .WithOne(e => e.DebitAccount)
            .HasForeignKey(e => e.DebitAccountId)
            .IsRequired();

        modelBuilder.Entity<Account>()
            .HasMany(e => e.Credits)
            .WithOne(e => e.CreditAccount)
            .HasForeignKey(e => e.CreditAccountId)
            .IsRequired();
    }
}

public class SqliteWalDbConnectionInterceptor : DbConnectionInterceptor
{
    private const string WalPragma = "PRAGMA journal_mode = 'wal';";

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = WalPragma;
        cmd.ExecuteNonQuery();
    }

    public override Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = WalPragma;
        return cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}

public class Account
{
    public int AccountId { get; set; }
    public decimal Balance { get; set; }
    public bool Frozen { get; set; }

    public ICollection<Transaction> Debits { get; set; }
    public ICollection<Transaction> Credits { get; set; }
}

public class Transaction
{
    public int TransactionId { get; set; }
    public decimal Amount { get; set; }
    public int DebitAccountId { get; set; }
    public Account DebitAccount { get; set; }
    public int CreditAccountId { get; set; }
    public Account CreditAccount { get; set; }
}