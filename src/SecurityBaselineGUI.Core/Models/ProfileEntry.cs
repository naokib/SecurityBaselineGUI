namespace SecurityBaselineGUI.Core.Models;

/// <summary>7章のProfilesテーブル1行に対応するモデル。ParametersJsonにタブ入力値一式を格納する。</summary>
public sealed class ProfileEntry
{
    public long Id { get; init; }
    public required string Name { get; set; }
    public required string ParametersJson { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; set; }
}
