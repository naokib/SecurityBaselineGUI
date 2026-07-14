using SecurityBaselineGUI.Core.Models;
using SecurityBaselineGUI.Core.Services;
using Xunit;

namespace SecurityBaselineGUI.Tests;

public sealed class BackupSettingsStoreTests : IDisposable
{
    private readonly string _tempDir;

    public BackupSettingsStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "SecurityBaselineGUI.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* ベストエフォート */ }
    }

    [Fact]
    public void Load_FileDoesNotExist_ReturnsDefaults()
    {
        var store = new BackupSettingsStore(Path.Combine(_tempDir, "missing.json"));

        var settings = store.Load();

        Assert.False(settings.Enabled);
        Assert.Equal(24, settings.IntervalHours);
        Assert.Null(settings.DestinationDirectory);
    }

    [Fact]
    public void Save_ThenLoad_RoundTrips()
    {
        var store = new BackupSettingsStore(Path.Combine(_tempDir, "backup-settings.json"));
        var original = new BackupSettings
        {
            Enabled = true,
            IntervalHours = 12,
            DestinationDirectory = @"D:\Backups\SecurityBaselineGUI",
            LastBackupUtc = new DateTimeOffset(2026, 7, 10, 9, 0, 0, TimeSpan.Zero),
        };

        store.Save(original);
        var loaded = store.Load();

        Assert.Equal(original.Enabled, loaded.Enabled);
        Assert.Equal(original.IntervalHours, loaded.IntervalHours);
        Assert.Equal(original.DestinationDirectory, loaded.DestinationDirectory);
        Assert.Equal(original.LastBackupUtc, loaded.LastBackupUtc);
    }
}
