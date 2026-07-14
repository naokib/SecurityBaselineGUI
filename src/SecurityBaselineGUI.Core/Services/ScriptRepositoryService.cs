using System.Security.Cryptography;
using System.Text;
using SecurityBaselineGUI.Core.Models;

namespace SecurityBaselineGUI.Core.Services;

/// <summary>
/// 仕様書4.5/5.3: 現行ps1のバージョン表示、新ps1とのパラメータ差分検出、
/// アーカイブへのバックアップ+差し替えを行う。
///
/// 注記: 本実装ではUiSchema.jsonによる動的UI生成(仕様書5.2)は未構築で、
/// GUIの各タブはパラメータ名を直接ハードコードして配線している。そのため
/// 差分検出後に「追加されたパラメータはGUIから入力できない」
/// 「削除されたパラメータをGUIが渡そうとすると実行時エラーになりうる」点を
/// 呼び出し側(スクリプト管理画面)で利用者に警告する必要がある。
/// </summary>
public sealed class ScriptRepositoryService
{
    private readonly ScriptParameterService _parameterService;

    public ScriptRepositoryService(ScriptParameterService parameterService) => _parameterService = parameterService;

    public ScriptVersionInfo GetVersionInfo(string scriptPath)
    {
        var content = File.ReadAllText(scriptPath);
        var version = ScriptVersionParser.ExtractVersion(content);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
        var lastModifiedUtc = File.GetLastWriteTimeUtc(scriptPath);
        return new ScriptVersionInfo(scriptPath, version, hash, lastModifiedUtc);
    }

    /// <summary>
    /// 新ps1の構文チェックを兼ねてパラメータ差分を計算する。
    /// 新ps1の構文が壊れている場合はScriptParameterServiceがInvalidOperationExceptionを投げる。
    /// </summary>
    public ParameterDiffResult CompareParameters(string currentScriptPath, string newScriptPath)
    {
        var oldParams = _parameterService.GetParameters(currentScriptPath);
        var newParams = _parameterService.GetParameters(newScriptPath);

        var oldByName = oldParams.ToDictionary(p => p.Name);
        var newByName = newParams.ToDictionary(p => p.Name);

        var added = newParams.Where(p => !oldByName.ContainsKey(p.Name)).ToList();
        var removed = oldParams.Where(p => !newByName.ContainsKey(p.Name)).ToList();
        var changed = oldParams
            .Where(op => newByName.ContainsKey(op.Name) && newByName[op.Name].TypeName != op.TypeName)
            .Select(op => new ParameterChange(op.Name, op.TypeName, newByName[op.Name].TypeName))
            .ToList();

        return new ParameterDiffResult { Added = added, Removed = removed, Changed = changed };
    }

    /// <summary>現行ps1をアーカイブへバックアップしたうえで、新ps1の内容に差し替える。戻り値はバックアップ先パス。</summary>
    public string ReplaceScript(string currentScriptPath, string newScriptPath, string archiveDirectory)
    {
        Directory.CreateDirectory(archiveDirectory);

        var currentVersion = GetVersionInfo(currentScriptPath).Version ?? "unknown";
        var safeVersion = string.Join("_", currentVersion.Split(Path.GetInvalidFileNameChars()));
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var baseName = Path.GetFileNameWithoutExtension(currentScriptPath);
        var archivePath = Path.Combine(archiveDirectory, $"{baseName}_{safeVersion}_{timestamp}.ps1");

        File.Copy(currentScriptPath, archivePath, overwrite: false);
        File.Copy(newScriptPath, currentScriptPath, overwrite: true);

        return archivePath;
    }
}
