using Microsoft.EntityFrameworkCore;
using SagaBank.Backend.Models;
using SagaBank.Shared;

namespace SagaBank.Backend;

public class BankContext : DbContext
{
    public DbSet<Account> Accounts { get; set; }
    public DbSet<InternalAccount> InternalAccounts { get; set; }
    public DbSet<ExternalAccount> ExternalAccounts { get; set; }

    public BankContext(DbContextOptions<BankContext> options) : base(options) { }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.AddInterceptors(new SqliteWalDbConnectionInterceptor());
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        //TPT
        //https://learn.microsoft.com/en-us/ef/core/modeling/inheritance#table-per-type-configuration
        //{
        //    modelBuilder.Entity<Account>()
        //        .ToTable("Accounts");
        //
        //    modelBuilder.Entity<InternalAccount>()
        //        .ToTable("InternalAccounts");
        //
        //    modelBuilder.Entity<ExternalAccount>()
        //        .ToTable("ExternalAccounts");
        //}

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

        modelBuilder.Entity<InternalAccount>()
            .Property(e => e.Balance)
            .HasDefaultValue(0)
            .IsRequired();

        modelBuilder.Entity<InternalAccount>()
            .Property(e => e.BalanceAvailable)
            .HasDefaultValue(0)
            .IsRequired();

        modelBuilder.Entity<Transaction>()
            .Property(e => e.TransactionId)
            .HasConversion<UlidToBytesConverter>();

        modelBuilder.Entity<Transaction>()
            .HasKey(e => e.TransactionId);

        modelBuilder.Entity<Transaction>()
            .Property(e => e.Amount)
            .IsRequired();
    }
}
