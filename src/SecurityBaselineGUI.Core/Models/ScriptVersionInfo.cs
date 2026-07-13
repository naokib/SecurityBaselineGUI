namespace SecurityBaselineGUI.Core.Models;

/// <summary>仕様書4.5/5.3: スクリプト管理画面に表示する現行ps1のバージョン情報。</summary>
public sealed record ScriptVersionInfo(string Path, string? Version, string Sha256, DateTimeOffset LastModifiedUtc);
