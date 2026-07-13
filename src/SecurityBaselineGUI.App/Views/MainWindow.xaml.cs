using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SecurityBaselineGUI.App.ViewModels;
using SecurityBaselineGUI.Core.Models;
using SecurityBaselineGUI.Core.Services;

namespace SecurityBaselineGUI.App.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly Func<CsvColumnCustomizeWindow> _csvColumnCustomizeWindowFactory;
    private readonly ScriptRepositoryService _scriptRepositoryService;

    public MainWindow(
        MainViewModel viewModel,
        Func<CsvColumnCustomizeWindow> csvColumnCustomizeWindowFactory,
        ScriptRepositoryService scriptRepositoryService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _csvColumnCustomizeWindowFactory = csvColumnCustomizeWindowFactory;
        _scriptRepositoryService = scriptRepositoryService;
        DataContext = _viewModel;
    }

    private void ProfileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox { SelectedItem: string name } && _viewModel.LoadProfileCommand.CanExecute(name))
        {
            _viewModel.LoadProfileCommand.Execute(name);
        }
    }

    private void CustomizeCsvColumnsButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = _csvColumnCustomizeWindowFactory();
        dialog.Owner = this;
        dialog.ShowDialog();
    }

    private async void ExportCsvButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "CSVファイル (*.csv)|*.csv",
            FileName = $"execution-history_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var csv = await _viewModel.BuildHistoryCsvAsync();
        await File.WriteAllTextAsync(dialog.FileName, csv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        MessageBox.Show(this, "CSVを出力しました。", "完了", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void RunButton_Click(object sender, RoutedEventArgs e)
    {
        var targets = _viewModel.SelectedOus.ToList();
        if (targets.Count == 0)
        {
            if (_viewModel.RunCommand.CanExecute(null))
            {
                _viewModel.RunCommand.Execute(null);
            }
            return;
        }

        var lines = new List<string>
        {
            "以下の内容でセキュリティベースラインを適用します。",
            string.Empty,
            $"対象OU数: {targets.Count}",
            $"GPOベース名: {_viewModel.GpoBaseName}",
            $"LAPSスキーマ拡張: {(_viewModel.PerformSchemaExtension && !_viewModel.IsLapsSchemaExtended ? "実行する" : "スキップ")}",
            $"LSA Protection UEFIロック: {(_viewModel.LsaProtectionUefiLock ? "有効" : "無効")}",
            $"ASRルール: {_viewModel.AsrRules.Count(r => r.IsEnabled)}件を設定対象",
            $"ASR除外パス: {_viewModel.ExclusionPaths.Count}件",
        };

        if (_viewModel.LsaProtectionUefiLock)
        {
            lines.Add(string.Empty);
            lines.Add("注意: UEFIロック付きのLSA Protectionは解除が難しいため、対象OUを必ず確認してください。");
        }

        lines.Add(string.Empty);
        lines.Add("実行しますか?");

        var result = MessageBox.Show(
            this,
            string.Join(Environment.NewLine, lines),
            "実行前の確認",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (result == MessageBoxResult.Yes && _viewModel.RunCommand.CanExecute(null))
        {
            _viewModel.RunCommand.Execute(null);
        }
    }

    private void UpdateScriptButton_Click(object sender, RoutedEventArgs e)
    {
        var openDialog = new OpenFileDialog
        {
            Filter = "PowerShellスクリプト (*.ps1)|*.ps1",
            Title = "更新するps1を選択",
        };
        if (openDialog.ShowDialog(this) != true)
        {
            return;
        }

        ParameterDiffResult diff;
        try
        {
            diff = _scriptRepositoryService.CompareParameters(_viewModel.ScriptPath, openDialog.FileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"新しいスクリプトの構文チェックに失敗しました。\n\n{ex.Message}",
                "構文エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var message = BuildDiffMessage(diff);
        var result = MessageBox.Show(this, message, "パラメータ差分の確認",
            MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            var archiveDir = Path.Combine(Path.GetDirectoryName(_viewModel.ScriptPath)!, "Archive");
            var archivePath = _scriptRepositoryService.ReplaceScript(_viewModel.ScriptPath, openDialog.FileName, archiveDir);
            _viewModel.RefreshScriptInfo();
            MessageBox.Show(this, $"スクリプトを更新しました。\n旧バージョンのバックアップ: {archivePath}",
                "完了", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"スクリプトの差し替えに失敗しました。\n\n{ex.Message}",
                "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string BuildDiffMessage(ParameterDiffResult diff)
    {
        if (!diff.HasChanges)
        {
            return "パラメータに変更はありません。差し替えを実行しますか?";
        }

        var lines = new List<string> { "以下のパラメータ差分が検出されました。" , string.Empty };

        if (diff.Added.Count > 0)
        {
            lines.Add($"[追加] {string.Join(", ", diff.Added.Select(p => p.Name))}");
            lines.Add("  → GUIのタブはパラメータ名を直接配線しているため、追加分は入力欄が無く実行時に渡されません。");
        }
        if (diff.Removed.Count > 0)
        {
            lines.Add($"[削除] {string.Join(", ", diff.Removed.Select(p => p.Name))}");
            lines.Add("  → GUIがこの名前で値を渡そうとしている場合、実行時にエラーになる可能性があります。");
        }
        if (diff.Changed.Count > 0)
        {
            lines.Add("[型変更] " + string.Join(", ", diff.Changed.Select(c => $"{c.Name}: {c.OldTypeName} → {c.NewTypeName}")));
        }

        lines.Add(string.Empty);
        lines.Add("差し替えを実行しますか?(現行スクリプトはArchiveフォルダに自動バックアップされます)");
        return string.Join(Environment.NewLine, lines);
    }

    private void BrowseBackupDestinationButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "バックアップ先フォルダーを選択" };
        if (dialog.ShowDialog(this) == true)
        {
            _viewModel.BackupDestination = dialog.FolderName;
        }
    }

    private void RestoreBackupButton_Click(object sender, RoutedEventArgs e)
    {
        var openDialog = new OpenFileDialog
        {
            Filter = "バックアップDBファイル (*.db)|*.db",
            Title = "復元するバックアップDBファイルを選択",
        };
        if (openDialog.ShowDialog(this) != true)
        {
            return;
        }

        var keyPath = openDialog.FileName + ".key.protected";
        if (!File.Exists(keyPath))
        {
            MessageBox.Show(this, $"対応する鍵ファイルが見つかりません。\n{keyPath}\n\nDBファイルと鍵ファイルは必ずペアで復元してください。",
                "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var confirm = MessageBox.Show(this,
            "現在のデータベースを選択したバックアップで上書きします。\n" +
            "(復元前に現在のDBの安全バックアップを自動的に作成します)\n\n" +
            "続行しますか?",
            "復元の確認", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            _viewModel.RestoreBackup(openDialog.FileName, keyPath);
            MessageBox.Show(this, "復元が完了しました。変更を反映するにはアプリを再起動してください。",
                "完了", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"復元に失敗しました。\n\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
