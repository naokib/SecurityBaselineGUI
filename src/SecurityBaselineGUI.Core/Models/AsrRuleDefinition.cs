namespace SecurityBaselineGUI.Core.Models;

/// <summary>Microsoft推奨ASRルール1件のカタログエントリ(GUIDと日本語表示用の説明)。</summary>
public sealed record AsrRuleDefinition(string Guid, string Name, string Description);
