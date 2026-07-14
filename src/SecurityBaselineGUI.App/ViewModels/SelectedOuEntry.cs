namespace SecurityBaselineGUI.App.ViewModels;

/// <summary>選択された対象OU1件。Nameは常にAD上の`name`属性(OUの葉の名前)。</summary>
public sealed record SelectedOuEntry(string DistinguishedName, string Name);
