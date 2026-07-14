using Microsoft.Data.Sqlite;
using SecurityBaselineGUI.Core.Models;

namespace SecurityBaselineGUI.Core.Services;

/// <summary>仕様書7.3 Profilesテーブルへのupsert/取得。</summary>
public sealed class ProfileStore
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public ProfileStore(SqliteConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task SaveAsync(string name, string parametersJson, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        var now = DateTimeOffset.Now.ToString("O");
        command.CommandText = """
            INSERT INTO Profiles (Name, ParametersJson, CreatedAt, UpdatedAt)
            VALUES ($name, $json, $now, $now)
            ON CONFLICT(Name) DO UPDATE SET ParametersJson = $json, UpdatedAt = $now;
            """;
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$json", parametersJson);
        command.Parameters.AddWithValue("$now", now);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ProfileEntry>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Name, ParametersJson, CreatedAt, UpdatedAt FROM Profiles ORDER BY Name;";

        var list = new List<ProfileEntry>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new ProfileEntry
            {
                Id = reader.GetInt64(0),
                Name = reader.GetString(1),
                ParametersJson = reader.GetString(2),
                CreatedAt = DateTimeOffset.Parse(reader.GetString(3)),
                UpdatedAt = DateTimeOffset.Parse(reader.GetString(4)),
            });
        }
        return list;
    }

    public async Task DeleteAsync(string name, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Profiles WHERE Name = $name;";
        command.Parameters.AddWithValue("$name", name);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
