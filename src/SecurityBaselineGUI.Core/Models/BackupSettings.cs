namespace SecurityBaselineGUI.Core.Models;

/// <summary>仕様書7.4: 定期バックアップの設定(有効/間隔/保存先/最終実行日時)。</summary>
public sealed class BackupSettings
{
    public bool Enabled { get; set; }
    public int IntervalHours { get; set; } = 24;
    public string? DestinationDirectory { get; set; }
    public DateTimeOffset? LastBackupUtc { get; set; }
}
