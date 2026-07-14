namespace SecurityBaselineGUI.Core.Models;

/// <summary>7章のExecutionHistoryテーブル1行に対応するモデル。</summary>
public sealed class ExecutionHistoryEntry
{
    public long Id { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required string UserName { get; init; }
    public required string TargetOU { get; init; }
    public required string GPOName { get; init; }
    public required string AsrRuleModesJson { get; init; }
    public required string ExclusionPathsJson { get; init; }
    public required bool Succeeded { get; init; }
    public required long DurationMs { get; init; }
    public required string LogText { get; init; }
    public required bool WasWhatIf { get; init; }
}
