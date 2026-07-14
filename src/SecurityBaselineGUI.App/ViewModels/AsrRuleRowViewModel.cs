using CommunityToolkit.Mvvm.ComponentModel;
using SecurityBaselineGUI.Core.Models;

namespace SecurityBaselineGUI.App.ViewModels;

/// <summary>ASRルール一覧DataGridの1行。仕様書4.1の「ルール単位個別トグル」に対応する。</summary>
public sealed partial class AsrRuleRowViewModel : ObservableObject
{
    public required string Guid { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }

    [ObservableProperty]
    private bool _isEnabled = true;

    [ObservableProperty]
    private AsrRuleMode _mode = AsrRuleMode.Audit;

    public static readonly IReadOnlyList<AsrRuleMode> AvailableModes =
    [
        AsrRuleMode.Audit,
        AsrRuleMode.Block,
        AsrRuleMode.Warn,
        AsrRuleMode.Disabled,
    ];

    /// <summary>ps1のAsrRuleModesハッシュテーブルに渡す実効モード(無効行はDisabled固定)。</summary>
    public AsrRuleMode EffectiveMode => IsEnabled ? Mode : AsrRuleMode.Disabled;
}
