using System.Text.Json;
using SecurityBaselineGUI.Core.Models;

namespace SecurityBaselineGUI.Core.Services;

/// <summary>
/// バックアップ設定の永続化先。プロファイル/実行履歴と異なりユーザーデータではなく
/// アプリ単体の構成値のため、SQLiteではなく単純なJSONファイルとして保存する。
/// </summary>
public sealed class BackupSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly string _filePath;

    public BackupSettingsStore(string filePath) => _filePath = filePath;

    public BackupSettings Load()
    {
        if (!File.Exists(_filePath))
        {
            return new BackupSettings();
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<BackupSettings>(json) ?? new BackupSettings();
        }
        catch (JsonException)
        {
            return new BackupSettings();
        }
    }

    public void Save(BackupSettings settings)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllText(_filePath, JsonSerializer.Serialize(settings, SerializerOptions));
    }
}
