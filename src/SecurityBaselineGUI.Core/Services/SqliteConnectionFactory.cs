using Microsoft.Data.Sqlite;

namespace SecurityBaselineGUI.Core.Services;

/// <summary>
/// 仕様書7章のDBファイルへの接続を作成し、スキーマを初期化する。
/// SQLCipher(SQLitePCLRaw.bundle_e_sqlcipher)でDBファイル自体を暗号化する。
/// 暗号化キーはDpapiKeyProtectorで保護されたものを呼び出し側(App起動処理)から渡す。
/// </summary>
public sealed class SqliteConnectionFactory
{
    private static readonly object InitLock = new();
    private static bool _initialized;

    private readonly string _connectionString;

    public SqliteConnectionFactory(string dbPath, byte[] encryptionKey)
    {
        EnsureSqlCipherProviderInitialized();

        var directory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            // SQLCipherの生鍵指定構文 x'<64桁hex>' を使う(パスフレーズKDFを介さず、
            // DpapiKeyProtectorが生成した32byteのランダムキーをそのまま暗号鍵として使う)。
            Password = $"x'{Convert.ToHexString(encryptionKey).ToLowerInvariant()}'",
        }.ToString();

        EnsureSchema();
    }

    public SqliteConnection CreateOpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private static void EnsureSqlCipherProviderInitialized()
    {
        if (_initialized)
        {
            return;
        }
        lock (InitLock)
        {
            if (_initialized)
            {
                return;
            }
            // SQLitePCLRaw.bundle_e_sqlcipher の Init() を最初に呼ぶことで、
            // プロセス全体でSQLCipherネイティブ実装(暗号化対応)を使わせる。
            // (Microsoft.Data.Sqlite.Core は暗号化非対応のe_sqlite3を同梱しないため、
            //  bundle_e_sqlite3 との二重初期化競合は起きない)
            SQLitePCL.Batteries_V2.Init();
            _initialized = true;
        }
    }

    private void EnsureSchema()
    {
        using var connection = CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS ExecutionHistory (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Timestamp TEXT NOT NULL,
                UserName TEXT NOT NULL,
                TargetOU TEXT NOT NULL,
                GPOName TEXT NOT NULL,
                AsrRuleModesJson TEXT NOT NULL,
                ExclusionPathsJson TEXT NOT NULL,
                Succeeded INTEGER NOT NULL,
                DurationMs INTEGER NOT NULL,
                LogText TEXT NOT NULL,
                WasWhatIf INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Profiles (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL UNIQUE,
                ParametersJson TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS CsvColumnPreferences (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ColumnName TEXT NOT NULL UNIQUE,
                DisplayOrder INTEGER NOT NULL,
                Visible INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS OperationJournal (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                OperationId TEXT NOT NULL,
                ProductId TEXT NOT NULL,
                OperationName TEXT NOT NULL,
                Stage TEXT NOT NULL,
                ConsistencyState TEXT NOT NULL,
                ExternalDependency TEXT NOT NULL,
                CorrelationKey TEXT NOT NULL,
                Message TEXT NOT NULL,
                OccurredAt TEXT NOT NULL,
                RecoveryRequired INTEGER NOT NULL,
                RepairPlan TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS IX_OperationJournal_OperationId
                ON OperationJournal(OperationId);

            CREATE INDEX IF NOT EXISTS IX_OperationJournal_RecoveryRequired
                ON OperationJournal(RecoveryRequired, OccurredAt DESC);
            """;
        command.ExecuteNonQuery();
    }
}
