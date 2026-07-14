using System.Windows;
using SecurityBaselineGUI.App.ViewModels;

namespace SecurityBaselineGUI.App.Views;

/// <summary>
/// 対象OUを選択する非モーダルの常時表示ウィンドウ。MainWindowの左側に配置され、
/// アプリ起動時に自動表示される(ボタン等での明示的な呼び出しは行わない)。
/// </summary>
public partial class OuSelectorWindow : Window
{
    public OuSelectorWindow(OuSelectorViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
