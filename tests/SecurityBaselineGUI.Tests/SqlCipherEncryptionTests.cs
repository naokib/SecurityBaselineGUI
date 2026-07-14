using Microsoft.Data.Sqlite;
using SecurityBaselineGUI.Core.Models;
using SecurityBaselineGUI.Core.Services;
using Xunit;

namespace SecurityBaselineGUI.Tests;

/// <summary>
/// 仕様書11章「DB関連テスト: DPAPI暗号化/復号のラウンドトリップ」に対応する検証。
/// SQLCipher統合が実際にファイルを暗号化していること(正しい鍵でのみ読める)を確認する。
/// </summary>
public sealed class SqlCipherEncryptionTests : IDisposable
{
    private readonly string _tempDir;

    public SqlCipherEncryptionTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "SecurityBaselineGUI.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* ベストエフォート */ }
    }

    [Fact]
    public async Task HistoryStore_RoundTripsThroughEncryptedDatabase()
    {
        var dbPath = Path.Combine(_tempDir, "app.db");
        var keyPath = Path.Combine(_tempDir, "app.db.key.protected");
        var key = new DpapiKeyProtector().LoadOrCreateProtectedKey(keyPath);

        var factory = new SqliteConnectionFactory(dbPath, key);
        var history = new HistoryStore(factory);

        var entry = new ExecutionHistoryEntry
        {
            Timestamp = DateTimeOffset.Now,
            UserName = "tester",
            TargetOU = "OU=Test,DC=contoso,DC=com",
            GPOName = "Security-Baseline-LAPS-LSA-ASR",
            AsrRuleModesJson = "{}",
            ExclusionPathsJson = "[]",
            Succeeded = true,
            DurationMs = 123,
            LogText = "dummy",
            WasWhatIf = true,
        };
        await history.InsertAsync(entry);

        var recent = await history.GetRecentAsync();
        Assert.Single(recent);
        Assert.Equal("tester", recent[0].UserName);
    }

    [Fact]
    public void EncryptedDatabase_CannotBeOpenedWithoutCorrectKey()
    {
        var dbPath = Path.Combine(_tempDir, "app.db");
        var keyPath = Path.Combine(_tempDir, "app.db.key.protected");
        var key = new DpapiKeyProtector().LoadOrCreateProtectedKey(keyPath);

        // データベースファイルを作成(スキーマ初期化のみ)
        _ = new SqliteConnectionFactory(dbPath, key);

        // 鍵なしで開いて読み取ろうとすると、暗号化されたファイルは平文のSQLiteとして
        // 解釈できないため失敗する(=実際に暗号化されていることの確認)。
        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT count(*) FROM sqlite_master;";

        Assert.Throws<SqliteException>(() => command.ExecuteScalar());
    }

    [Fact]
    public void EncryptedDatabase_CannotBeOpenedWithWrongKey()
    {
        var dbPath = Path.Combine(_tempDir, "app.db");
        var keyPath = Path.Combine(_tempDir, "app.db.key.protected");
        var correctKey = new DpapiKeyProtector().LoadOrCreateProtectedKey(keyPath);
        _ = new SqliteConnectionFactory(dbPath, correctKey);

        var wrongKey = new byte[correctKey.Length];
        Array.Copy(correctKey, wrongKey, wrongKey.Length);
        wrongKey[0] ^= 0xFF; // 1バイトだけ変えて別鍵にする

        // SqliteConnectionFactoryのコンストラクタはEnsureSchema()でスキーマ読み取りを
        // 行うため、誤った鍵ではコンストラクタの時点で例外になる。
        Assert.Throws<SqliteException>(() => new SqliteConnectionFactory(dbPath, wrongKey));
    }
}
