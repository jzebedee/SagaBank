using Microsoft.EntityFrameworkCore;
using SagaBank.Shared;
using SagaBank.Shared.Models;

namespace SagaBank.Credits;

public class CreditContext : DbContext
{
    public DbSet<Credit> Credits { get; set; }

    public CreditContext(DbContextOptions<CreditContext> options) : base(options) { }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.AddInterceptors(new SqliteWalDbConnectionInterceptor());
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Credit>()
            .Property(e => e.TransactionId)
            .HasConversion<UlidToBytesConverter>();

        modelBuilder.Entity<Credit>()
            .HasKey(e => e.TransactionId);

        modelBuilder.Entity<Credit>()
            .Property(e => e.Amount)
            .IsRequired();

        modelBuilder.Entity<Credit>()
            .Property(e => e.CreditAccountId)
            .IsRequired();

        modelBuilder.Entity<Credit>()
            .Property(e => e.Timestamp)
            .IsRequired();
    }
}
