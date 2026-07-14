using SecurityBaselineGUI.Core.Models;
using SecurityBaselineGUI.Core.Services;
using Xunit;

namespace SecurityBaselineGUI.Tests;

public sealed class CsvExportServiceTests
{
    private static ExecutionHistoryEntry CreateEntry(string userName = "tester", string exclusionPathsJson = "[]") => new()
    {
        Timestamp = new DateTimeOffset(2026, 7, 10, 9, 30, 0, TimeSpan.Zero),
        UserName = userName,
        TargetOU = "OU=Workstations,DC=contoso,DC=com",
        GPOName = "Security-Baseline-LAPS-LSA-ASR",
        AsrRuleModesJson = """{"a":"Block","b":"Block","c":"Audit"}""",
        ExclusionPathsJson = exclusionPathsJson,
        Succeeded = true,
        DurationMs = 4200,
        LogText = "dummy",
        WasWhatIf = false,
    };

    [Fact]
    public void BuildCsv_WritesHeaderAndOrdersSelectedColumnsOnly()
    {
        var service = new CsvExportService();
        var csv = service.BuildCsv([CreateEntry()], ["Succeeded", "UserName"]);

        var lines = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("結果,実行ユーザー", lines[0]);
        Assert.Equal("成功,tester", lines[1]);
    }

    [Fact]
    public void BuildCsv_EscapesValuesContainingCommasAndQuotes()
    {
        var service = new CsvExportService();
        var csv = service.BuildCsv([CreateEntry(userName: "a,\"b\"")], ["UserName"]);

        var lines = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("\"a,\"\"b\"\"\"", lines[1]);
    }

    [Fact]
    public void BuildCsv_SummarizesAsrRuleModesAndExclusionPathCount()
    {
        var service = new CsvExportService();
        var csv = service.BuildCsv([CreateEntry(exclusionPathsJson: """["C:\\Temp\\a","C:\\Temp\\b"]""")],
            ["AsrRuleModesSummary", "ExclusionPathCount"]);

        var lines = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("Block:2 / Audit:1,2", lines[1]);
    }

    [Fact]
    public void BuildCsv_IgnoresUnknownColumnKeys()
    {
        var service = new CsvExportService();
        var csv = service.BuildCsv([CreateEntry()], ["NoSuchColumn", "UserName"]);

        var lines = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("実行ユーザー", lines[0]);
    }
}
