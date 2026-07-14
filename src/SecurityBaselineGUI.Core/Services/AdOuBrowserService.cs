using System.DirectoryServices;
using System.Runtime.InteropServices;
using SecurityBaselineGUI.Core.Models;

namespace SecurityBaselineGUI.Core.Services;

/// <summary>
/// 基本タブのTargetOU入力欄向けのADツリーピッカーが使う、OU階層の1階層ぶんの取得サービス。
/// 大規模ADを想定し、常に1階層ぶん(OneLevel)だけを都度取得する遅延ロード前提の設計。
/// </summary>
public sealed class AdOuBrowserService
{
    /// <summary>現在のドメインのルートDN(defaultNamingContext)を返す。</summary>
    public string GetDefaultNamingContext()
    {
        try
        {
            using var rootDse = new DirectoryEntry("LDAP://RootDSE");
            var value = rootDse.Properties["defaultNamingContext"].Value as string;
            if (string.IsNullOrEmpty(value))
            {
                throw new InvalidOperationException("defaultNamingContext が取得できませんでした。");
            }
            return value;
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException)
        {
            throw new AdOuBrowserException(
                "Active Directoryに接続できませんでした。ドメイン参加済みの端末で実行しているか、ADへの到達性を確認してください。",
                ex);
        }
    }

    /// <summary>指定DN直下(1階層のみ)のOU一覧を名前順で返す。</summary>
    public IReadOnlyList<OuNode> GetChildOrganizationalUnits(string parentDistinguishedName)
    {
        try
        {
            using var parent = new DirectoryEntry($"LDAP://{parentDistinguishedName}");
            using var searcher = new DirectorySearcher(parent)
            {
                Filter = "(objectClass=organizationalUnit)",
                SearchScope = SearchScope.OneLevel,
                PageSize = 1000,
            };
            searcher.PropertiesToLoad.Add("name");
            searcher.PropertiesToLoad.Add("distinguishedName");
            searcher.Sort = new SortOption("name", SortDirection.Ascending);

            using var results = searcher.FindAll();
            var nodes = new List<OuNode>(results.Count);
            foreach (SearchResult result in results)
            {
                var name = result.Properties["name"].Count > 0 ? result.Properties["name"][0]?.ToString() : null;
                var dn = result.Properties["distinguishedName"].Count > 0 ? result.Properties["distinguishedName"][0]?.ToString() : null;
                if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(dn))
                {
                    nodes.Add(new OuNode(name, dn));
                }
            }
            return nodes;
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException)
        {
            throw new AdOuBrowserException($"'{parentDistinguishedName}' 配下のOU一覧の取得に失敗しました。", ex);
        }
    }
}
