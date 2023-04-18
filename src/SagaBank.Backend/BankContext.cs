using Microsoft.EntityFrameworkCore;
using SagaBank.Backend.Models;
using SagaBank.Shared;

namespace SagaBank.Backend;

public class BankContext : DbContext
{
    public DbSet<Account> Accounts { get; set; }

    public BankContext(DbContextOptions<BankContext> options) : base(options) { }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.AddInterceptors(new SqliteWalDbConnectionInterceptor());
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>()
            .Property(e => e.Balance)
            .HasDefaultValue(0)
            .IsRequired();
    }
}
