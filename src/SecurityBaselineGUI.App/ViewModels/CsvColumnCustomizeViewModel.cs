using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SecurityBaselineGUI.Core.Models;
using SecurityBaselineGUI.Core.Services;

namespace SecurityBaselineGUI.App.ViewModels;

/// <summary>仕様書4.3: 履歴CSVエクスポートの出力列を選択・並び替えするダイアログのVM。</summary>
public sealed partial class CsvColumnCustomizeViewModel : ObservableObject
{
    private readonly CsvColumnPreferenceStore _store;

    public ObservableCollection<CsvColumnOptionViewModel> Columns { get; } = [];

    [ObservableProperty] private CsvColumnOptionViewModel? _selectedColumn;

    public CsvColumnCustomizeViewModel(CsvColumnPreferenceStore store)
    {
        _store = store;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        var saved = await _store.GetAllAsync();
        var catalogByKey = CsvColumnCatalog.AllColumns.ToDictionary(c => c.Key);

        foreach (var pref in saved.OrderBy(p => p.DisplayOrder))
        {
            if (catalogByKey.TryGetValue(pref.ColumnKey, out var def))
            {
                Columns.Add(new CsvColumnOptionViewModel { Key = def.Key, DisplayName = def.DisplayName, IsVisible = pref.Visible });
            }
        }

        // 保存済み設定に無い列(カタログへの新規追加分)は表示状態で末尾に追加する。
        foreach (var def in CsvColumnCatalog.AllColumns)
        {
            if (Columns.All(c => c.Key != def.Key))
            {
                Columns.Add(new CsvColumnOptionViewModel { Key = def.Key, DisplayName = def.DisplayName, IsVisible = true });
            }
        }
    }

    [RelayCommand]
    private void MoveUp()
    {
        if (SelectedColumn is null)
        {
            return;
        }
        var index = Columns.IndexOf(SelectedColumn);
        if (index > 0)
        {
            Columns.Move(index, index - 1);
        }
    }

    [RelayCommand]
    private void MoveDown()
    {
        if (SelectedColumn is null)
        {
            return;
        }
        var index = Columns.IndexOf(SelectedColumn);
        if (index >= 0 && index < Columns.Count - 1)
        {
            Columns.Move(index, index + 1);
        }
    }

    public async Task SaveAsync()
    {
        var preferences = Columns
            .Select((c, index) => new CsvColumnPreference { ColumnKey = c.Key, DisplayOrder = index, Visible = c.IsVisible })
            .ToList();
        await _store.ReplaceAllAsync(preferences);
    }
}
