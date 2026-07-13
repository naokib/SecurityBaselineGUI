using SecurityBaselineGUI.Core.Models;

namespace SecurityBaselineGUI.Core.Services;

/// <summary>仕様書7.4: 「今、定期バックアップを実行すべきか」の純粋な判定ロジック(副作用なし・単体テスト容易)。</summary>
public static class BackupSchedule
{
    public static bool IsDue(BackupSettings settings, DateTimeOffset now)
    {
        if (!settings.Enabled || string.IsNullOrWhiteSpace(settings.DestinationDirectory))
        {
            return false;
        }
        if (settings.LastBackupUtc is null)
        {
            return true;
        }
        return now - settings.LastBackupUtc.Value >= TimeSpan.FromHours(settings.IntervalHours);
    }
}
