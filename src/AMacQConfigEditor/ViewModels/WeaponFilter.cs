using System;
using System.Collections.Generic;

namespace AMacQConfigEditor.ViewModels;

/// <summary>
/// 枪械列表筛选规则，主窗口搜索框与 Ctrl+Alt+M 快速切换窗口共用：
/// 多个空格分隔的关键词按"与"匹配，每个关键词对内部代码、显示名、已绑按键摘要做子串匹配。
/// </summary>
internal static class WeaponFilter
{
    public static string[] SplitTerms(string? query) =>
        string.IsNullOrWhiteSpace(query)
            ? []
            : query!.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

    public static bool Matches(string? name, string? displayName, string? summary, string term) =>
        Contains(name, term) || Contains(displayName, term) || Contains(summary, term);

    /// <summary>按关键词逐个过滤，关键词之间是"与"关系；没有任何关键词时保留全部。</summary>
    public static List<T> Apply<T>(IEnumerable<T> source, string? query, Func<T, string?> name, Func<T, string?> displayName, Func<T, string?> summary)
    {
        var terms = SplitTerms(query);
        var matched = new List<(T Item, int Rank)>();
        foreach (var item in source)
        {
            var itemName = name(item);
            var itemDisplayName = displayName(item);
            var itemSummary = summary(item);

            var isMatch = true;
            var rank = 0;
            foreach (var term in terms)
            {
                var termRank = MatchRank(itemName, itemDisplayName, itemSummary, term);
                if (termRank < 0)
                {
                    isMatch = false;
                    break;
                }
                rank += termRank;
            }

            if (isMatch) matched.Add((item, rank));
        }

        // 同分时保留原始顺序，保证列表顺序稳定、Enter 的目标可预期。
        return matched
            .Select((entry, index) => (entry.Item, entry.Rank, Index: index))
            .OrderBy(entry => entry.Rank)
            .ThenBy(entry => entry.Index)
            .Select(entry => entry.Item)
            .ToList();
    }

    /// <summary>
    /// 命中优先级：内部代码完全一致 &gt; 内部代码前缀 &gt; 显示名完全一致 &gt; 显示名前缀
    /// &gt; 内部代码子串 &gt; 显示名子串 &gt; 按键摘要子串。返回负数表示未命中。
    /// 这样输入 "mk4" 会先给出 MK4 而不是排在配置里更靠前的 MK47。
    /// </summary>
    private static int MatchRank(string? name, string? displayName, string? summary, string term)
    {
        if (string.Equals(name, term, StringComparison.OrdinalIgnoreCase)) return 0;
        if (StartsWith(name, term)) return 1;
        if (string.Equals(displayName, term, StringComparison.OrdinalIgnoreCase)) return 2;
        if (StartsWith(displayName, term)) return 3;
        if (Contains(name, term)) return 4;
        if (Contains(displayName, term)) return 5;
        if (Contains(summary, term)) return 6;
        return -1;
    }

    private static bool Contains(string? source, string term) =>
        source is not null && source.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;

    private static bool StartsWith(string? source, string term) =>
        source is not null && source.StartsWith(term, StringComparison.OrdinalIgnoreCase);
}
