namespace SecurityBaselineGUI.Core.Models;

/// <summary>仕様書7.3 CsvColumnPreferencesテーブル1行(履歴CSVエクスポートの列カスタマイズ設定)。</summary>
public sealed class CsvColumnPreference
{
    public required string ColumnKey { get; init; }
    public required int DisplayOrder { get; set; }
    public required bool Visible { get; set; }
}
