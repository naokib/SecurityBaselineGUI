using System.DirectoryServices;
using System.Runtime.InteropServices;

namespace SecurityBaselineGUI.Core.Services;

/// <summary>
/// Windows LAPS用にADスキーマが拡張済みか(ms-LAPS-Password属性の有無)を確認する。
/// ps1側の判定ロジック(Get-ADObject -SearchBase schemaNamingContext)と同じ考え方を、
/// GUI側でも実行前に確認できるようにLDAPで直接判定する。
/// </summary>
public sealed class LapsSchemaStatusService
{
    public bool IsSchemaExtended()
    {
        try
        {
            using var rootDse = new DirectoryEntry("LDAP://RootDSE");
            var schemaNamingContext = rootDse.Properties["schemaNamingContext"].Value as string;
            if (string.IsNullOrEmpty(schemaNamingContext))
            {
                throw new InvalidOperationException("schemaNamingContext が取得できませんでした。");
            }

            using var schemaEntry = new DirectoryEntry($"LDAP://{schemaNamingContext}");
            using var searcher = new DirectorySearcher(schemaEntry)
            {
                Filter = "(name=ms-LAPS-Password)",
                SearchScope = SearchScope.OneLevel,
            };
            searcher.PropertiesToLoad.Add("name");

            return searcher.FindOne() is not null;
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException)
        {
            throw new AdOuBrowserException(
                "ADスキーマの状態を確認できませんでした。ドメイン参加済みの端末で実行しているか、ADへの到達性を確認してください。",
                ex);
        }
    }
}
