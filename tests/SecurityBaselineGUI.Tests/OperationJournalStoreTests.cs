using SecurityBaselineGUI.Core.Models;
using SecurityBaselineGUI.Core.Services;
using Xunit;

namespace SecurityBaselineGUI.Tests;

public sealed class OperationJournalStoreTests
{
    [Fact]
    public async Task AddAsync_StoresRecoveryRequiredEntry()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"sbg-journal-{Guid.NewGuid():N}.db");
        var factory = new SqliteConnectionFactory(dbPath, Enumerable.Range(0, 32).Select(i => (byte)i).ToArray());
        var store = new OperationJournalStore(factory);
        var entry = new OperationJournalEntry(
            "op-001",
            "security-baseline-gui",
            "GPO apply",
            "PowerShellExecution",
            "PartialApply",
            "AD/GPO",
            "OU=Test,DC=example,DC=local",
            "PowerShell interrupted after partial OU apply.",
            DateTimeOffset.UtcNow,
            true,
            "Run WhatIf and compare actual linked GPO state before reapply.");

        await store.AddAsync(entry);

        var recoveryEntries = await store.GetRecoveryRequiredAsync();
        Assert.Single(recoveryEntries);
        Assert.Equal("op-001", recoveryEntries[0].OperationId);
        Assert.Equal("PartialApply", recoveryEntries[0].ConsistencyState);
    }
}
