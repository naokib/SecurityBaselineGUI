using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SecurityBaselineGUI.Core.Services;

namespace SecurityBaselineGUI.App.ViewModels;

/// <summary>
/// 左側の独立ウィンドウ(OuSelectorWindow)のVM。ADのOUツリーをチェックボックスで
/// 複数選択でき、選択結果はSelectedOuStore(共有状態)へ即座に反映される。
/// これによりMainWindow側は常に「現在作業中のOU」を参照できる。
/// </summary>
public sealed partial class OuSelectorViewModel : ObservableObject
{
    private readonly AdOuBrowserService _browserService;
    private readonly SelectedOuStore _selectedOuStore;

    public ObservableCollection<OuTreeNodeViewModel> RootNodes { get; } = [];
    public ObservableCollection<SelectedOuEntry> SelectedOus => _selectedOuStore.SelectedOus;

    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isInitializing = true;

    public string GpoBaseName
    {
        get => _selectedOuStore.GpoBaseName;
        set
        {
            if (_selectedOuStore.GpoBaseName != value)
            {
                _selectedOuStore.GpoBaseName = value;
                OnPropertyChanged();
            }
        }
    }

    public OuSelectorViewModel(AdOuBrowserService browserService, SelectedOuStore selectedOuStore)
    {
        _browserService = browserService;
        _selectedOuStore = selectedOuStore;
        _selectedOuStore.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SelectedOuStore.GpoBaseName))
            {
                OnPropertyChanged(nameof(GpoBaseName));
            }
        };

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            var rootDn = await Task.Run(() => _browserService.GetDefaultNamingContext());
            var rootNode = new OuTreeNodeViewModel(_browserService, rootDn, rootDn, isSelectable: false, OnNodeCheckedChanged);
            RootNodes.Add(rootNode);
            rootNode.IsExpanded = true;
        }
        catch (AdOuBrowserException ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsInitializing = false;
        }
    }

    private void OnNodeCheckedChanged(OuTreeNodeViewModel node)
    {
        if (node.IsChecked)
        {
            _selectedOuStore.Add(new SelectedOuEntry(node.DistinguishedName, node.Name));
        }
        else
        {
            _selectedOuStore.Remove(node.DistinguishedName);
        }
    }

    [RelayCommand]
    private void ClearSelection()
    {
        foreach (var node in EnumerateAllNodes(RootNodes))
        {
            node.IsChecked = false;
        }
        _selectedOuStore.SelectedOus.Clear();
    }

    [RelayCommand]
    private void RemoveSelected(SelectedOuEntry entry)
    {
        _selectedOuStore.Remove(entry.DistinguishedName);
        var node = EnumerateAllNodes(RootNodes).FirstOrDefault(n => n.DistinguishedName == entry.DistinguishedName);
        if (node is not null)
        {
            node.IsChecked = false;
        }
    }

    private static IEnumerable<OuTreeNodeViewModel> EnumerateAllNodes(IEnumerable<OuTreeNodeViewModel> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            foreach (var child in EnumerateAllNodes(node.Children))
            {
                yield return child;
            }
        }
    }
}
