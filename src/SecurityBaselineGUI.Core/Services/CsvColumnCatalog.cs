using System.Text.Json;
using SecurityBaselineGUI.Core.Models;

namespace SecurityBaselineGUI.Core.Services;

/// <summary>
/// 仕様書4.3: 履歴CSVエクスポートで選択できる列の一覧(既定列セット)。
/// 実装が進んだ段階で決定するとされていたが(仕様書13章)、履歴テーブルの実データに
/// 基づき本カタログを既定セットとして採用する。
/// </summary>
public static class CsvColumnCatalog
{
    public static readonly IReadOnlyList<CsvColumnDefinition> AllColumns =
    [
        new("Timestamp", "実行日時", e => e.Timestamp.ToString("yyyy-MM-dd HH:mm:ss")),
        new("UserName", "実行ユーザー", e => e.UserName),
        new("TargetOU", "TargetOU", e => e.TargetOU),
        new("GPOName", "GPOName", e => e.GPOName),
        new("AsrRuleModesSummary", "ASRルールモード(集計)", SummarizeAsrRuleModes),
        new("ExclusionPathCount", "除外パス数", e => CountJsonArray(e.ExclusionPathsJson).ToString()),
        new("WasWhatIf", "WhatIf", e => e.WasWhatIf ? "Yes" : "No"),
        new("Succeeded", "結果", e => e.Succeeded ? "成功" : "失敗"),
        new("DurationMs", "所要時間(ms)", e => e.DurationMs.ToString()),
    ];

    /// <summary>初回起動時に使う既定の列順・表示状態(カタログの並び順で全列表示)。</summary>
    public static IReadOnlyList<CsvColumnPreference> CreateDefaultPreferences() =>
        AllColumns
            .Select((c, index) => new CsvColumnPreference { ColumnKey = c.Key, DisplayOrder = index, Visible = true })
            .ToList();

    private static string SummarizeAsrRuleModes(ExecutionHistoryEntry entry)
    {
        try
        {
            var modes = JsonSerializer.Deserialize<Dictionary<string, string>>(entry.AsrRuleModesJson);
            if (modes is null || modes.Count == 0)
            {
                return string.Empty;
            }
            return string.Join(" / ", modes.Values
                .GroupBy(v => v)
                .OrderByDescending(g => g.Count())
                .Select(g => $"{g.Key}:{g.Count()}"));
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private static int CountJsonArray(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<string[]>(json)?.Length ?? 0;
        }
        catch (JsonException)
        {
            return 0;
        }
    }
}
