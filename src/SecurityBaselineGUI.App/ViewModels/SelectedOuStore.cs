using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SecurityBaselineGUI.App.ViewModels;

/// <summary>
/// OU選択ウィンドウとMainWindowが共有する「現在作業中のOU」状態。
/// OU選択を独立ウィンドウに切り出したことで、両ウィンドウが同じ選択状態と
/// GPOベース名を参照できるよう、DIでシングルトンとして共有する。
/// </summary>
public sealed partial class SelectedOuStore : ObservableObject
{
    public ObservableCollection<SelectedOuEntry> SelectedOus { get; } = [];

    [ObservableProperty]
    private string _gpoBaseName = "Security-Baseline-LAPS-LSA-ASR";

    public void Add(SelectedOuEntry entry)
    {
        if (!SelectedOus.Any(e => e.DistinguishedName == entry.DistinguishedName))
        {
            SelectedOus.Add(entry);
        }
    }

    public void Remove(string distinguishedName)
    {
        var existing = SelectedOus.FirstOrDefault(e => e.DistinguishedName == distinguishedName);
        if (existing is not null)
        {
            SelectedOus.Remove(existing);
        }
    }

    /// <summary>指定OUに対する実行時GPO名(GPOベース名 + OU名)を計算する。</summary>
    public string ComputeGpoName(SelectedOuEntry entry) => $"{GpoBaseName}-{entry.Name}";
}
