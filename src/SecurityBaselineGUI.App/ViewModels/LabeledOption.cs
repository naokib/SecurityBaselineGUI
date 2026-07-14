namespace SecurityBaselineGUI.App.ViewModels;

/// <summary>ComboBox表示用の値+説明ラベルのペア。</summary>
public sealed record LabeledOption<T>(T Value, string Label)
{
    public override string ToString() => Label;
}
