using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AMacQConfigEditor.Services;

public static class LuaConfigService
{
    /// <summary>
    /// 赋值行：<c>变量名 = 值</c>。变量名允许连字符，因为枪械标识里存在 AKS-74 / CI-19 这类写法
    /// （纯 Lua 标识符不允许连字符，但配置文件里确实这么写，因此这里放宽）。
    /// </summary>
    private static readonly Regex AssignmentPattern = new(
        @"^(?<indent>\s*)(?<name>[A-Za-z_][A-Za-z0-9_-]*)\s*=\s*(?<value>[^\r\n]*)\r?$",
        RegexOptions.Multiline | RegexOptions.CultureInvariant);

    /// <summary>
    /// 枪械变量名形如 <c>&lt;枪械&gt;_qq1156777787</c> / <c>_qq1156777787_second</c> / <c>_Third</c>。
    /// 枪械部分允许连字符（配置里存在 AKS-74 / CI-19 / SV-98 / SR-25 这类写法），
    /// 但不允许下划线——否则无法确定哪一段是枪械名、哪一段是槽位后缀。
    /// </summary>
    private static readonly Regex PrimaryWeaponPattern = new(
        @"^(?<weapon>[A-Za-z0-9][A-Za-z0-9-]*)_(?:qq1156777787|qq1156777787_second|Third)$",
        RegexOptions.CultureInvariant);

    /// <summary>
    /// 已知的全局变量名。它们不会带槽位后缀，列在这里只是为了让"某个全局变量恰好叫
    /// xxx_Third"这类情况不被误认成枪械，避免列表里出现幽灵条目。
    /// </summary>
    private static readonly HashSet<string> ReservedNames = new(StringComparer.Ordinal)
    {
        "press", "modeswitch", "off", "pause", "second", "Third", "BingXi", "sjddx", "sjddy"
    };

    private static readonly (string Suffix, string Prefix)[] BindingDescriptors =
    [
        ("qq1156777787", string.Empty),
        ("qq1156777787_second", "Alt+"),
        ("Third", "Ctrl+")
    ];

    public static LuaDocument CreateDocument(string content) => new(content);

    public static IReadOnlyList<LuaAssignment> GetAssignments(string content) => ParseAssignments(content);

    public static IReadOnlyList<string> GetPrimaryWeapons(string content) =>
        ParseAssignments(content)
            .Select(assignment => PrimaryWeaponPattern.Match(assignment.Name))
            .Where(match => match.Success)
            .Select(match => match.Groups["weapon"].Value)
            .Where(weapon => !ReservedNames.Contains(weapon))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    public static string? GetNumber(string content, string variableName) =>
        ParseNumber(ParseAssignments(content).FirstOrDefault(assignment => assignment.Name == variableName)?.Value);

    public static string? GetString(string content, string variableName)
    {
        var match = Regex.Match(content, $"(?m)^\\s*{Regex.Escape(variableName)}\\s*=\\s*['\\\"](?<value>[^'\\\"]*)['\\\"]\\r?$");
        return match.Success ? match.Groups["value"].Value : null;
    }

    private static string? ParseNumber(string? value)
    {
        var number = value is null ? null : Regex.Match(value, @"^-?(?:\d+(?:\.\d{1,2})?|\.\d{1,2})");
        return number is { Success: true } ? number.Value : null;
    }

    private static IReadOnlyList<LuaAssignment> ParseAssignments(string content)
    {
        if (content is null) throw new ArgumentNullException(nameof(content));
        return AssignmentPattern.Matches(content)
            .Cast<Match>()
            .Select(match => new LuaAssignment(match.Groups["name"].Value, match.Groups["value"].Value.Trim()))
            .ToArray();
    }

    private static string SetValue(string content, string variableName, string value, bool quoted)
    {
        if (content is null) throw new ArgumentNullException(nameof(content));
        if (string.IsNullOrWhiteSpace(variableName)) throw new ArgumentException("Variable name cannot be null or whitespace.", nameof(variableName));
        if (value is null) throw new ArgumentNullException(nameof(value));

        var valuePattern = quoted
            ? "(?<quote>[\"'])(?<value>[^\"\\r\\n]*?)\\k<quote>"
            : @"(?<value>-?(?:\d+(?:\.\d{1,2})?|\.\d{1,2}))";
        var pattern = new Regex(
            $@"^(?<prefix>\s*{Regex.Escape(variableName)}\s*=\s*){valuePattern}(?<suffix>[^\r\n]*)(?<lineEnd>\r?\n|$)",
            RegexOptions.Multiline | RegexOptions.CultureInvariant);
        return pattern.Replace(content, match =>
        {
            var replacementValue = quoted
                ? $"{match.Groups["quote"].Value}{value.Replace(match.Groups["quote"].Value, $"\\{match.Groups["quote"].Value}")}{match.Groups["quote"].Value}"
                : value;
            return $"{match.Groups["prefix"].Value}{replacementValue}{match.Groups["suffix"].Value}{match.Groups["lineEnd"].Value}";
        }, count: 1);
    }

    /// <summary>
    /// 一份 Lua 配置的内存视图。整份文档只扫描一次，变量名到取值的映射被缓存下来，
    /// 因此“切换枪械 / 刷新列表 / 保存”这类高频操作不再反复遍历全文。
    /// 一批修改会先攒在待写入表里，由 <see cref="ApplyPendingChanges"/> 一次性应用，
    /// 保证最终写回文件的内容与逐条修改原字符串的结果一致。
    /// </summary>
    public sealed class LuaDocument
    {
        private readonly Dictionary<string, string> _pendingNumbers = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _pendingStrings = new(StringComparer.Ordinal);
        private Dictionary<string, string>? _valuesByName;

        public LuaDocument(string content) => Content = content;

        public string Content { get; private set; }

        public string? GetNumber(string variableName) =>
            ValuesByName.TryGetValue(variableName, out var value) ? ParseNumber(value) : null;

        public string? GetString(string variableName)
        {
            if (_pendingStrings.TryGetValue(variableName, out var pending)) return pending;

            // 引号内的值在赋值解析里会带着引号，因此字符串读取仍按原有的精确行匹配完成。
            var match = Regex.Match(Content, $"(?m)^\\s*{Regex.Escape(variableName)}\\s*=\\s*['\\\"](?<value>[^'\\\"]*)['\\\"]\\r?$");
            return match.Success ? match.Groups["value"].Value : null;
        }

        public string GetBindingSummary(string weapon) =>
            string.Join(" · ", BindingDescriptors
                .Select(binding => (binding.Prefix, Value: GetNumber($"{weapon}_{binding.Suffix}")))
                .Where(binding => binding.Value is not null && binding.Value is not "0")
                .Select(binding => $"{binding.Prefix}{binding.Value}"));

        public LuaDocument SetNumber(string variableName, string value)
        {
            _pendingNumbers[variableName] = value;
            return this;
        }

        /// <summary>
        /// 与旧实现保持一致：字符串写入只会改写“原本带引号”的赋值行，
        /// 因此文件里写成裸值（如 modeswitch = scrolllock）时行为不变。
        /// </summary>
        public LuaDocument SetString(string variableName, string value)
        {
            _pendingStrings[variableName] = value;
            return this;
        }

        /// <summary>
        /// 把当前文档里所有“非本枪械但占用了同一按键位”的绑定清零。候选集合基于本次
        /// 批量修改生效后的内容，因此连续调用（如依次写入三组按键后再清理冲突）结果
        /// 与逐条修改原字符串一致。
        /// </summary>
        public LuaDocument ClearConflictingBinding(string selectedWeapon, string suffix, string selectedValue)
        {
            if (selectedValue == "0") return this;

            // 先取一次映射（可能触发待写入生效并重建缓存），再直接遍历它：
            // 循环体只改 _pendingNumbers，不会动这份映射，因此无需再复制一份。
            var valuesByName = ValuesByName;
            foreach (var assignment in valuesByName)
            {
                var name = assignment.Key;
                var separator = name.IndexOf('_');
                if (separator <= 0) continue;

                var weapon = name.Substring(0, separator);
                var assignmentSuffix = name.Substring(separator + 1);
                if (string.Equals(weapon, selectedWeapon, StringComparison.Ordinal) || assignmentSuffix != suffix) continue;
                if (ParseNumber(assignment.Value) == selectedValue) _pendingNumbers[name] = "0";
            }

            return this;
        }

        /// <summary>
        /// 应用并合并一批修改：按变量名分组的连续替换一次完成，避免每个变量都重扫全文。
        /// 返回应用后的完整文档内容。
        /// </summary>
        public string ApplyPendingChanges()
        {
            ApplyPendingChangesCore();
            return Content;
        }

        private Dictionary<string, string> ValuesByName
        {
            get
            {
                ApplyPendingChangesCore();
                return _valuesByName ??= ParseAssignments(Content).ToDictionary(assignment => assignment.Name, assignment => assignment.Value, StringComparer.Ordinal);
            }
        }

        private void ApplyPendingChangesCore()
        {
            if (_pendingNumbers.Count == 0 && _pendingStrings.Count == 0) return;

            var content = Content;
            foreach (var change in _pendingNumbers) content = SetValue(content, change.Key, change.Value, quoted: false);
            foreach (var change in _pendingStrings) content = SetValue(content, change.Key, change.Value, quoted: true);

            _pendingNumbers.Clear();
            _pendingStrings.Clear();
            Content = content;
            _valuesByName = null;
        }
    }
}

public sealed record LuaAssignment(string Name, string Value);
