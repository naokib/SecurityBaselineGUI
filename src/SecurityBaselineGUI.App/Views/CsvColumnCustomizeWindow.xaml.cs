using System.Windows;
using SecurityBaselineGUI.App.ViewModels;

namespace SecurityBaselineGUI.App.Views;

public partial class CsvColumnCustomizeWindow : Window
{
    private readonly CsvColumnCustomizeViewModel _viewModel;

    public CsvColumnCustomizeWindow(CsvColumnCustomizeViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    private async void OkButton_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.SaveAsync();
        DialogResult = true;
    }
}
