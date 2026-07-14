using CommunityToolkit.Mvvm.ComponentModel;

namespace SecurityBaselineGUI.App.ViewModels;

/// <summary>CSV列カスタマイズダイアログの1行(キー・表示名・表示有無)。</summary>
public sealed partial class CsvColumnOptionViewModel : ObservableObject
{
    public required string Key { get; init; }
    public required string DisplayName { get; init; }

    [ObservableProperty]
    private bool _isVisible = true;
}
