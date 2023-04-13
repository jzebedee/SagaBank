using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace SagaBank.Shared;

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
