using Microsoft.EntityFrameworkCore;
using SagaBank.Shared;

namespace SagaBank.Debits;

public class DebitContext : DbContext
{
    public DbSet<Debit> Debits { get; set; }

    public DebitContext(DbContextOptions<DebitContext> options) : base(options) { }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.AddInterceptors(new SqliteWalDbConnectionInterceptor());
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Debit>()
            .HasKey(e => e.TransactionId);

        modelBuilder.Entity<Debit>()
            .Property(e => e.Amount)
            .IsRequired();

        modelBuilder.Entity<Debit>()
            .Property(e => e.DebitAccountId)
            .IsRequired();

        modelBuilder.Entity<Debit>()
            .Property(e => e.Timestamp)
            .IsRequired();
    }
}

public class Debit
{
    public byte[] TransactionId { get; set; }
    public int DebitAccountId { get; set; }
    public decimal Amount { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}