namespace SecurityBaselineGUI.Core.Models;

/// <summary>履歴CSVエクスポートで選択可能な列1つの定義(キー、表示名、値の取り出し方)。</summary>
public sealed record CsvColumnDefinition(string Key, string DisplayName, Func<ExecutionHistoryEntry, string> ValueSelector);
