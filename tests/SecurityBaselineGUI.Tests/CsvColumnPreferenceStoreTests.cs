using SecurityBaselineGUI.Core.Models;
using SecurityBaselineGUI.Core.Services;
using Xunit;

namespace SecurityBaselineGUI.Tests;

public sealed class CsvColumnPreferenceStoreTests : IDisposable
{
    private readonly string _tempDir;

    public CsvColumnPreferenceStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "SecurityBaselineGUI.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* ベストエフォート */ }
    }

    private SqliteConnectionFactory CreateFactory()
    {
        var dbPath = Path.Combine(_tempDir, "app.db");
        var keyPath = Path.Combine(_tempDir, "app.db.key.protected");
        var key = new DpapiKeyProtector().LoadOrCreateProtectedKey(keyPath);
        return new SqliteConnectionFactory(dbPath, key);
    }

    [Fact]
    public async Task ReplaceAllAsync_ThenGetAllAsync_RoundTripsInDisplayOrder()
    {
        var store = new CsvColumnPreferenceStore(CreateFactory());

        var preferences = new[]
        {
            new CsvColumnPreference { ColumnKey = "UserName", DisplayOrder = 1, Visible = true },
            new CsvColumnPreference { ColumnKey = "Timestamp", DisplayOrder = 0, Visible = false },
        };
        await store.ReplaceAllAsync(preferences);

        var loaded = await store.GetAllAsync();

        Assert.Equal(2, loaded.Count);
        Assert.Equal("Timestamp", loaded[0].ColumnKey);
        Assert.False(loaded[0].Visible);
        Assert.Equal("UserName", loaded[1].ColumnKey);
        Assert.True(loaded[1].Visible);
    }

    [Fact]
    public async Task ReplaceAllAsync_CalledTwice_DoesNotAccumulateRows()
    {
        var store = new CsvColumnPreferenceStore(CreateFactory());

        await store.ReplaceAllAsync(CsvColumnCatalog.CreateDefaultPreferences());
        await store.ReplaceAllAsync(CsvColumnCatalog.CreateDefaultPreferences());

        var loaded = await store.GetAllAsync();
        Assert.Equal(CsvColumnCatalog.AllColumns.Count, loaded.Count);
    }
}
