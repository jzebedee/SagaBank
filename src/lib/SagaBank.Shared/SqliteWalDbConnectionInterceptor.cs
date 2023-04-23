using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace SagaBank.Shared;

public class SqliteWalDbConnectionInterceptor : DbConnectionInterceptor
{
    private const string WalPragma = "PRAGMA journal_mode = 'wal';";
    private const string SyncNormalPragma = "PRAGMA synchronous = 'normal';";

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = WalPragma;
        cmd.ExecuteNonQuery();
        cmd.CommandText = SyncNormalPragma;
        cmd.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = WalPragma;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
        cmd.CommandText = SyncNormalPragma;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
