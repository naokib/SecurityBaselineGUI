using SecurityBaselineGUI.Core.Services;
using Xunit;

namespace SecurityBaselineGUI.Tests;

public sealed class DatabaseBackupServiceTests : IDisposable
{
    private readonly string _tempDir;

    public DatabaseBackupServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "SecurityBaselineGUI.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* ベストエフォート */ }
    }

    [Fact]
    public void CreateBackup_CopiesDbAndKeyAsMatchingPair()
    {
        var dbPath = Path.Combine(_tempDir, "app.db");
        var keyPath = Path.Combine(_tempDir, "app.db.key.protected");
        File.WriteAllText(dbPath, "dummy-db-content");
        File.WriteAllText(keyPath, "dummy-key-content");
        var destination = Path.Combine(_tempDir, "backups");

        var service = new DatabaseBackupService();
        var result = service.CreateBackup(dbPath, keyPath, destination);

        Assert.True(File.Exists(result.DatabaseBackupPath));
        Assert.True(File.Exists(result.KeyBackupPath));
        Assert.Equal(result.DatabaseBackupPath + ".key.protected", result.KeyBackupPath);
        Assert.Equal("dummy-db-content", File.ReadAllText(result.DatabaseBackupPath));
        Assert.Equal("dummy-key-content", File.ReadAllText(result.KeyBackupPath));
    }

    [Fact]
    public void CreateBackup_MissingKeyFile_Throws()
    {
        var dbPath = Path.Combine(_tempDir, "app.db");
        File.WriteAllText(dbPath, "dummy");
        var missingKeyPath = Path.Combine(_tempDir, "missing.key.protected");

        var service = new DatabaseBackupService();

        Assert.Throws<FileNotFoundException>(() => service.CreateBackup(dbPath, missingKeyPath, Path.Combine(_tempDir, "backups")));
    }

    [Fact]
    public void RestoreBackup_OverwritesTargetFiles()
    {
        var backupDb = Path.Combine(_tempDir, "backup.db");
        var backupKey = Path.Combine(_tempDir, "backup.db.key.protected");
        File.WriteAllText(backupDb, "restored-db");
        File.WriteAllText(backupKey, "restored-key");

        var targetDb = Path.Combine(_tempDir, "live", "app.db");
        var targetKey = Path.Combine(_tempDir, "live", "app.db.key.protected");
        Directory.CreateDirectory(Path.GetDirectoryName(targetDb)!);
        File.WriteAllText(targetDb, "old-db");
        File.WriteAllText(targetKey, "old-key");

        var service = new DatabaseBackupService();
        service.RestoreBackup(backupDb, backupKey, targetDb, targetKey);

        Assert.Equal("restored-db", File.ReadAllText(targetDb));
        Assert.Equal("restored-key", File.ReadAllText(targetKey));
    }

    [Fact]
    public void RestoreBackup_MissingBackupKeyFile_ThrowsAndDoesNotTouchTarget()
    {
        var backupDb = Path.Combine(_tempDir, "backup.db");
        File.WriteAllText(backupDb, "restored-db");
        var missingBackupKey = Path.Combine(_tempDir, "missing.db.key.protected");

        var targetDb = Path.Combine(_tempDir, "live", "app.db");
        var targetKey = Path.Combine(_tempDir, "live", "app.db.key.protected");
        Directory.CreateDirectory(Path.GetDirectoryName(targetDb)!);
        File.WriteAllText(targetDb, "old-db");
        File.WriteAllText(targetKey, "old-key");

        var service = new DatabaseBackupService();

        Assert.Throws<FileNotFoundException>(() => service.RestoreBackup(backupDb, missingBackupKey, targetDb, targetKey));
        Assert.Equal("old-db", File.ReadAllText(targetDb));
    }
}
