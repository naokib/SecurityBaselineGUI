namespace SecurityBaselineGUI.Core.Models;

/// <summary>1回のバックアップ実行結果(DBファイルと鍵ファイルは常にペアで扱う)。</summary>
public sealed record BackupResult(string DatabaseBackupPath, string KeyBackupPath, DateTimeOffset Timestamp);
