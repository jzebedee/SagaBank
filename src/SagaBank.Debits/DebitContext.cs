﻿using Microsoft.EntityFrameworkCore;
using SagaBank.Shared;
using SagaBank.Shared.Models;

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
            .Property(e => e.TransactionId)
            .HasConversion<UlidToBytesConverter>();

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
