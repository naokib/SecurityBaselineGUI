using Microsoft.Data.Sqlite;
using SecurityBaselineGUI.Core.Models;

namespace SecurityBaselineGUI.Core.Services;

/// <summary>仕様書7.3 CsvColumnPreferencesテーブルへのCRUD(全置き換え方式)。</summary>
public sealed class CsvColumnPreferenceStore
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public CsvColumnPreferenceStore(SqliteConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<IReadOnlyList<CsvColumnPreference>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT ColumnName, DisplayOrder, Visible FROM CsvColumnPreferences ORDER BY DisplayOrder;";

        var list = new List<CsvColumnPreference>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new CsvColumnPreference
            {
                ColumnKey = reader.GetString(0),
                DisplayOrder = reader.GetInt32(1),
                Visible = reader.GetInt64(2) != 0,
            });
        }
        return list;
    }

    /// <summary>設定一式を丸ごと置き換える(件数が少ないため差分更新はせず全削除→再挿入)。</summary>
    public async Task ReplaceAllAsync(IReadOnlyList<CsvColumnPreference> preferences, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var transaction = connection.BeginTransaction();

        using (var deleteCommand = connection.CreateCommand())
        {
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText = "DELETE FROM CsvColumnPreferences;";
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach (var preference in preferences)
        {
            using var insertCommand = connection.CreateCommand();
            insertCommand.Transaction = transaction;
            insertCommand.CommandText = """
                INSERT INTO CsvColumnPreferences (ColumnName, DisplayOrder, Visible)
                VALUES ($columnName, $displayOrder, $visible);
                """;
            insertCommand.Parameters.AddWithValue("$columnName", preference.ColumnKey);
            insertCommand.Parameters.AddWithValue("$displayOrder", preference.DisplayOrder);
            insertCommand.Parameters.AddWithValue("$visible", preference.Visible ? 1 : 0);
            await insertCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}
