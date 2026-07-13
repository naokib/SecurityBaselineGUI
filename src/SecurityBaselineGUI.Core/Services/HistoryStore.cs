using Microsoft.Data.Sqlite;
using SecurityBaselineGUI.Core.Models;

namespace SecurityBaselineGUI.Core.Services;

/// <summary>仕様書7.3 ExecutionHistoryテーブルへのCRUD。</summary>
public sealed class HistoryStore
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public HistoryStore(SqliteConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<long> InsertAsync(ExecutionHistoryEntry entry, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO ExecutionHistory
                (Timestamp, UserName, TargetOU, GPOName, AsrRuleModesJson, ExclusionPathsJson, Succeeded, DurationMs, LogText, WasWhatIf)
            VALUES
                ($timestamp, $userName, $targetOU, $gpoName, $asrRuleModesJson, $exclusionPathsJson, $succeeded, $durationMs, $logText, $wasWhatIf);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$timestamp", entry.Timestamp.ToString("O"));
        command.Parameters.AddWithValue("$userName", entry.UserName);
        command.Parameters.AddWithValue("$targetOU", entry.TargetOU);
        command.Parameters.AddWithValue("$gpoName", entry.GPOName);
        command.Parameters.AddWithValue("$asrRuleModesJson", entry.AsrRuleModesJson);
        command.Parameters.AddWithValue("$exclusionPathsJson", entry.ExclusionPathsJson);
        command.Parameters.AddWithValue("$succeeded", entry.Succeeded ? 1 : 0);
        command.Parameters.AddWithValue("$durationMs", entry.DurationMs);
        command.Parameters.AddWithValue("$logText", entry.LogText);
        command.Parameters.AddWithValue("$wasWhatIf", entry.WasWhatIf ? 1 : 0);

        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt64(result);
    }

    public async Task<IReadOnlyList<ExecutionHistoryEntry>> GetRecentAsync(int limit = 200, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Timestamp, UserName, TargetOU, GPOName, AsrRuleModesJson, ExclusionPathsJson, Succeeded, DurationMs, LogText, WasWhatIf
            FROM ExecutionHistory
            ORDER BY Id DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", limit);

        var list = new List<ExecutionHistoryEntry>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new ExecutionHistoryEntry
            {
                Id = reader.GetInt64(0),
                Timestamp = DateTimeOffset.Parse(reader.GetString(1)),
                UserName = reader.GetString(2),
                TargetOU = reader.GetString(3),
                GPOName = reader.GetString(4),
                AsrRuleModesJson = reader.GetString(5),
                ExclusionPathsJson = reader.GetString(6),
                Succeeded = reader.GetInt64(7) != 0,
                DurationMs = reader.GetInt64(8),
                LogText = reader.GetString(9),
                WasWhatIf = reader.GetInt64(10) != 0,
            });
        }
        return list;
    }
}
