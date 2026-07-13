using SecurityBaselineGUI.Core.Models;

namespace SecurityBaselineGUI.Core.Services;

/// <summary>
/// 仕様書7.4: 暗号化DBファイルとDPAPI保護済み鍵ファイルを常にペアでバックアップ/復元する。
/// 鍵ファイル単体・DBファイル単体のバックアップは、片方だけでは復号できず意味を持たないため許可しない。
/// </summary>
public sealed class DatabaseBackupService
{
    private const string KeyFileSuffix = ".key.protected";

    /// <summary>DB+鍵ファイルを指定フォルダへコピーする。戻り値はコピー先のパス2つとタイムスタンプ。</summary>
    public BackupResult CreateBackup(string dbPath, string keyPath, string destinationDirectory)
    {
        if (!File.Exists(dbPath))
        {
            throw new FileNotFoundException("バックアップ対象のDBファイルが見つかりません。", dbPath);
        }
        if (!File.Exists(keyPath))
        {
            throw new FileNotFoundException("バックアップ対象の鍵ファイルが見つかりません。", keyPath);
        }

        Directory.CreateDirectory(destinationDirectory);

        var timestamp = DateTimeOffset.Now;
        var baseName = $"{Path.GetFileNameWithoutExtension(dbPath)}_{timestamp:yyyyMMdd_HHmmss}";
        var dbBackupPath = Path.Combine(destinationDirectory, $"{baseName}.db");
        var keyBackupPath = dbBackupPath + KeyFileSuffix;

        File.Copy(dbPath, dbBackupPath, overwrite: false);
        File.Copy(keyPath, keyBackupPath, overwrite: false);

        return new BackupResult(dbBackupPath, keyBackupPath, timestamp);
    }

    /// <summary>選択されたバックアップDB+鍵ファイルで、現在の実運用ファイルを上書きする。</summary>
    public void RestoreBackup(string backupDbPath, string backupKeyPath, string targetDbPath, string targetKeyPath)
    {
        if (!File.Exists(backupDbPath))
        {
            throw new FileNotFoundException("復元元のDBファイルが見つかりません。", backupDbPath);
        }
        if (!File.Exists(backupKeyPath))
        {
            throw new FileNotFoundException(
                "復元元の鍵ファイルが見つかりません。DBファイルと鍵ファイルは必ずペアで復元してください。", backupKeyPath);
        }

        var targetDir = Path.GetDirectoryName(targetDbPath);
        if (!string.IsNullOrEmpty(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        File.Copy(backupDbPath, targetDbPath, overwrite: true);
        File.Copy(backupKeyPath, targetKeyPath, overwrite: true);
    }
}
