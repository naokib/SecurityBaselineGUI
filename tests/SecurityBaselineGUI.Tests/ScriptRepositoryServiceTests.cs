using SecurityBaselineGUI.Core.Services;
using Xunit;

namespace SecurityBaselineGUI.Tests;

public sealed class ScriptRepositoryServiceTests : IDisposable
{
    private readonly string _tempDir;

    public ScriptRepositoryServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "SecurityBaselineGUI.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* ベストエフォート */ }
    }

    private static string RealScriptPath => Path.Combine(AppContext.BaseDirectory, "TestData", "Configure-SecurityBaseline.ps1");

    private const string BaseScript = """
        <#
        .SYNOPSIS
            テスト用スクリプト
        .NOTES
            Version: 1.0.0
        #>
        param(
            [Parameter(Mandatory)]
            [string]$TargetOU,

            [switch]$SkipSchemaExtension,

            [ValidateRange(14, 64)]
            [int]$LapsPasswordLength = 20
        )
        Write-Output "dummy"
        """;

    [Fact]
    public void GetVersionInfo_ExtractsVersionAndComputesHash()
    {
        var service = new ScriptRepositoryService(new ScriptParameterService());

        var info = service.GetVersionInfo(RealScriptPath);

        Assert.Equal("1.2.0", info.Version);
        Assert.Equal(64, info.Sha256.Length);
    }

    [Fact]
    public void CompareParameters_ComparedAgainstItself_HasNoChanges()
    {
        var service = new ScriptRepositoryService(new ScriptParameterService());

        var diff = service.CompareParameters(RealScriptPath, RealScriptPath);

        Assert.False(diff.HasChanges);
    }

    [Fact]
    public void CompareParameters_DetectsAddedRemovedAndChangedParameters()
    {
        var oldPath = Path.Combine(_tempDir, "old.ps1");
        var newPath = Path.Combine(_tempDir, "new.ps1");
        File.WriteAllText(oldPath, BaseScript);

        var newScript = """
            <#
            .NOTES
                Version: 1.1.0
            #>
            param(
                [Parameter(Mandatory)]
                [string]$TargetOU,

                [string]$LapsPasswordLength = "20",

                [switch]$NewFeatureFlag
            )
            Write-Output "dummy"
            """;
        File.WriteAllText(newPath, newScript);

        var service = new ScriptRepositoryService(new ScriptParameterService());
        var diff = service.CompareParameters(oldPath, newPath);

        Assert.True(diff.HasChanges);
        Assert.Contains(diff.Added, p => p.Name == "NewFeatureFlag");
        Assert.Contains(diff.Removed, p => p.Name == "SkipSchemaExtension");
        Assert.Contains(diff.Changed, c => c.Name == "LapsPasswordLength" && c.OldTypeName == "Int32" && c.NewTypeName == "String");
    }

    [Fact]
    public void ReplaceScript_BacksUpOldContentAndAppliesNewContent()
    {
        var currentPath = Path.Combine(_tempDir, "Configure-SecurityBaseline.ps1");
        var newPath = Path.Combine(_tempDir, "new.ps1");
        var archiveDir = Path.Combine(_tempDir, "Archive");

        File.WriteAllText(currentPath, BaseScript);
        File.WriteAllText(newPath, BaseScript.Replace("1.0.0", "2.0.0"));

        var service = new ScriptRepositoryService(new ScriptParameterService());
        var archivePath = service.ReplaceScript(currentPath, newPath, archiveDir);

        Assert.True(File.Exists(archivePath));
        Assert.Contains("Version: 1.0.0", File.ReadAllText(archivePath));
        Assert.Contains("Version: 2.0.0", File.ReadAllText(currentPath));
    }
}
