using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace SagaBank.Shared.Contexts;

public class MessageBoxContext : DbContext
{
    public DbSet<InboxMessage> Inbox { get; set; }
    public DbSet<OutboxMessage> Outbox { get; set; }

    public MessageBoxContext(DbContextOptions<MessageBoxContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        const string Timestamp = "datetime('now')";

        modelBuilder.Entity<InboxMessage>()
            .Property(e => e.Id)
            .HasConversion<UlidToBytesConverter>();

        modelBuilder.Entity<InboxMessage>()
            .HasKey(e => new { e.Id, e.Type });

        modelBuilder.Entity<InboxMessage>()
            .Property(e => e.Added)
            .HasDefaultValueSql(Timestamp)
            .ValueGeneratedOnAdd();

        modelBuilder.Entity<OutboxMessage>()
            .Property(e => e.Id)
            .HasConversion<UlidToBytesConverter>();

        modelBuilder.Entity<OutboxMessage>()
            .HasKey(e => new { e.Id, e.Type });

        modelBuilder.Entity<OutboxMessage>()
            .Property(e => e.Added)
            .HasDefaultValueSql(Timestamp)
            .ValueGeneratedOnAdd();

        modelBuilder.Entity<OutboxMessage>()
            .Property(e => e.State)
            .HasDefaultValue(OutboxMessageState.NotSent)
            .IsRequired();

        modelBuilder.Entity<OutboxMessage>()
            .Property(e => e.Payload)
            .IsRequired();
    }

    public void Process(InboxMessage incoming, Func<IDbContextTransaction, OutboxMessage> action)
    {
        using var dbTx = Database.BeginTransaction();

        Inbox.Add(incoming);
        SaveChanges();

        var outgoing = action(dbTx);
        ArgumentNullException.ThrowIfNull(outgoing);

        Outbox.Add(outgoing);
        SaveChanges();

        dbTx.Commit();
    }

    public void Process(InboxMessage incoming, Action<IDbContextTransaction> action)
    {
        using var dbTx = Database.BeginTransaction();

        Inbox.Add(incoming);
        SaveChanges();

        action(dbTx);

        dbTx.Commit();
    }
}

public class InboxMessage
{
    public Ulid Id { get; set; }
    public string Type { get; set; }
    public DateTimeOffset Added { get; set; }
}

public class OutboxMessage
{
    public Ulid Id { get; set; }
    public string Type { get; set; }
    public DateTimeOffset Added { get; set; }
    public byte[] Payload { get; set; }
    public OutboxMessageState State { get; set; }
}

public enum OutboxMessageState
{
    NotSent,
    Sent
}