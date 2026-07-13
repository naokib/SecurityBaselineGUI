using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SecurityBaselineGUI.Core.Services;

namespace SecurityBaselineGUI.App.ViewModels;

/// <summary>
/// ADツリーピッカーのTreeView1ノード。展開(IsExpanded)時に初めて
/// 子OUをAdOuBrowserServiceから1階層ぶんだけ遅延取得する。
/// チェックボックスによる複数選択(IsChecked)に対応し、変更をonCheckedChangedで呼び出し元へ通知する。
/// </summary>
public sealed partial class OuTreeNodeViewModel : ObservableObject
{
    private readonly AdOuBrowserService? _browserService;
    private readonly Action<OuTreeNodeViewModel>? _onCheckedChanged;
    private bool _childrenLoaded;

    public string Name { get; }
    public string DistinguishedName { get; }
    public bool IsPlaceholder { get; }

    /// <summary>ドメインルート等、実際のOUではないためチェック不可のノードはfalse。</summary>
    public bool IsSelectable { get; }

    public ObservableCollection<OuTreeNodeViewModel> Children { get; } = [];

    [ObservableProperty] private bool _isExpanded;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isChecked;

    public OuTreeNodeViewModel(
        AdOuBrowserService browserService,
        string name,
        string distinguishedName,
        bool isSelectable,
        Action<OuTreeNodeViewModel>? onCheckedChanged)
    {
        _browserService = browserService;
        _onCheckedChanged = onCheckedChanged;
        Name = name;
        DistinguishedName = distinguishedName;
        IsSelectable = isSelectable;
        Children.Add(CreatePlaceholder("読み込み中..."));
    }

    private OuTreeNodeViewModel(string displayText)
    {
        Name = displayText;
        DistinguishedName = string.Empty;
        IsPlaceholder = true;
    }

    private static OuTreeNodeViewModel CreatePlaceholder(string text) => new(text);

    partial void OnIsExpandedChanged(bool value)
    {
        if (value && !_childrenLoaded && !IsPlaceholder)
        {
            _ = LoadChildrenAsync();
        }
    }

    partial void OnIsCheckedChanged(bool value)
    {
        if (!IsPlaceholder && IsSelectable)
        {
            _onCheckedChanged?.Invoke(this);
        }
    }

    private async Task LoadChildrenAsync()
    {
        if (_childrenLoaded || _browserService is null)
        {
            return;
        }
        _childrenLoaded = true;
        IsLoading = true;
        try
        {
            var children = await Task.Run(() => _browserService.GetChildOrganizationalUnits(DistinguishedName));
            Children.Clear();
            foreach (var child in children)
            {
                Children.Add(new OuTreeNodeViewModel(_browserService, child.Name, child.DistinguishedName, isSelectable: true, _onCheckedChanged));
            }
        }
        catch (AdOuBrowserException ex)
        {
            Children.Clear();
            Children.Add(CreatePlaceholder($"エラー: {ex.Message}"));
        }
        finally
        {
            IsLoading = false;
        }
    }
}
