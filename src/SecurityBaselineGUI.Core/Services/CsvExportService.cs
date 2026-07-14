using System.Text;
using SecurityBaselineGUI.Core.Models;

namespace SecurityBaselineGUI.Core.Services;

/// <summary>仕様書4.3: 履歴一覧を、選択・並び替え済みの列でCSV文字列に変換する。</summary>
public sealed class CsvExportService
{
    public string BuildCsv(IEnumerable<ExecutionHistoryEntry> entries, IReadOnlyList<string> orderedVisibleColumnKeys)
    {
        var columnsByKey = CsvColumnCatalog.AllColumns.ToDictionary(c => c.Key);
        var columns = orderedVisibleColumnKeys
            .Where(columnsByKey.ContainsKey)
            .Select(key => columnsByKey[key])
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", columns.Select(c => Escape(c.DisplayName))));
        foreach (var entry in entries)
        {
            sb.AppendLine(string.Join(",", columns.Select(c => Escape(c.ValueSelector(entry)))));
        }
        return sb.ToString();
    }

    private static string Escape(string value)
    {
        if (value.IndexOfAny([',', '"', '\n', '\r']) < 0)
        {
            return value;
        }
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
