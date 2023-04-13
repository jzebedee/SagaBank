using Microsoft.EntityFrameworkCore;
using SagaBank.Backend.Models;
using SagaBank.Shared;

namespace SagaBank.Backend;

public class AccountContext : DbContext
{
    public DbSet<Account> Accounts { get; set; }

    public AccountContext(DbContextOptions<AccountContext> options) : base(options) { }

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
