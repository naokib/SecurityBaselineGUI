namespace SecurityBaselineGUI.Core.Models;

/// <summary>型が変わったパラメータ1件(名前は変わらず型のみ変化)。</summary>
public sealed record ParameterChange(string Name, string OldTypeName, string NewTypeName);

/// <summary>仕様書4.5 手順3: 新旧ps1のparam()ブロックを比較した結果。</summary>
public sealed class ParameterDiffResult
{
    public required IReadOnlyList<ParameterDescriptor> Added { get; init; }
    public required IReadOnlyList<ParameterDescriptor> Removed { get; init; }
    public required IReadOnlyList<ParameterChange> Changed { get; init; }

    public bool HasChanges => Added.Count > 0 || Removed.Count > 0 || Changed.Count > 0;
}
