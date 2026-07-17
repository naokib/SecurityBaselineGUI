using Microsoft.Data.Sqlite;
using SecurityBaselineGUI.Core.Models;

namespace SecurityBaselineGUI.Core.Services;

public sealed class OperationJournalStore
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public OperationJournalStore(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task AddAsync(OperationJournalEntry entry, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO OperationJournal
                (OperationId, ProductId, OperationName, Stage, ConsistencyState, ExternalDependency, CorrelationKey, Message, OccurredAt, RecoveryRequired, RepairPlan)
            VALUES
                ($operationId, $productId, $operationName, $stage, $consistencyState, $externalDependency, $correlationKey, $message, $occurredAt, $recoveryRequired, $repairPlan);
            """;
        command.Parameters.AddWithValue("$operationId", entry.OperationId);
        command.Parameters.AddWithValue("$productId", entry.ProductId);
        command.Parameters.AddWithValue("$operationName", entry.OperationName);
        command.Parameters.AddWithValue("$stage", entry.Stage);
        command.Parameters.AddWithValue("$consistencyState", entry.ConsistencyState);
        command.Parameters.AddWithValue("$externalDependency", entry.ExternalDependency);
        command.Parameters.AddWithValue("$correlationKey", entry.CorrelationKey);
        command.Parameters.AddWithValue("$message", entry.Message);
        command.Parameters.AddWithValue("$occurredAt", entry.OccurredAt.ToString("O"));
        command.Parameters.AddWithValue("$recoveryRequired", entry.RecoveryRequired ? 1 : 0);
        command.Parameters.AddWithValue("$repairPlan", entry.RepairPlan);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<OperationJournalEntry>> GetRecoveryRequiredAsync(
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT OperationId, ProductId, OperationName, Stage, ConsistencyState, ExternalDependency, CorrelationKey, Message, OccurredAt, RecoveryRequired, RepairPlan
            FROM OperationJournal
            WHERE RecoveryRequired = 1
            ORDER BY OccurredAt DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", limit);

        var entries = new List<OperationJournalEntry>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            entries.Add(ReadEntry(reader));
        }

        return entries;
    }

    private static OperationJournalEntry ReadEntry(SqliteDataReader reader) => new(
        reader.GetString(0),
        reader.GetString(1),
        reader.GetString(2),
        reader.GetString(3),
        reader.GetString(4),
        reader.GetString(5),
        reader.GetString(6),
        reader.GetString(7),
        DateTimeOffset.Parse(reader.GetString(8)),
        reader.GetInt64(9) != 0,
        reader.GetString(10));
}
