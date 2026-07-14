using System.Text.RegularExpressions;

namespace SecurityBaselineGUI.Core.Services;

/// <summary>ps1のコメントベースヘルプ .NOTES 内 "Version: x.y.z" を抜き出す。</summary>
public static partial class ScriptVersionParser
{
    [GeneratedRegex(@"Version:\s*(\S+)")]
    private static partial Regex VersionRegex();

    public static string? ExtractVersion(string scriptContent)
    {
        var match = VersionRegex().Match(scriptContent);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }
}
