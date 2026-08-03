using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AMacQConfigEditor.Services;

public static class LuaConfigService
{
    private static readonly Regex AssignmentPattern = new(
        @"^(?<indent>\s*)(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?<value>[^\r\n]*)\r?$",
        RegexOptions.Multiline | RegexOptions.CultureInvariant);

    public static string SetNumber(string content, string variableName, string value) =>
        SetValue(content, variableName, value, quoted: false);

    public static string SetString(string content, string variableName, string value) =>
        SetValue(content, variableName, value, quoted: true);

    public static string ClearConflictingBindings(string content, string selectedWeapon, IReadOnlyDictionary<string, string> suffixValues)
    {
        foreach (var assignment in GetAssignments(content))
        {
            var separator = assignment.Name.IndexOf('_');
            if (separator <= 0 || assignment.Value == "0")
            {
                continue;
            }

            var weapon = assignment.Name.Substring(0, separator);
            var suffix = assignment.Name.Substring(separator + 1);
            if (!string.Equals(weapon, selectedWeapon, StringComparison.Ordinal) &&
                suffixValues.TryGetValue(suffix, out var selectedValue) &&
                selectedValue != "0" &&
                assignment.Value == selectedValue)
            {
                content = SetNumber(content, assignment.Name, "0");
            }
        }

        return content;
    }

    public static IReadOnlyList<LuaAssignment> GetAssignments(string content) =>
        AssignmentPattern.Matches(content)
            .Cast<Match>()
            .Select(match => new LuaAssignment(match.Groups["name"].Value, match.Groups["value"].Value.Trim()))
            .ToArray();

    public static IReadOnlyList<string> GetPrimaryWeapons(string content) =>
        GetAssignments(content)
            .Select(assignment => assignment.Name)
            .Select(name => Regex.Match(name, @"^(?<weapon>[A-Za-z0-9]+)_(qq1156777787|qq1156777787_second|Third)$"))
            .Where(match => match.Success)
            .Select(match => match.Groups["weapon"].Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    public static string? GetNumber(string content, string variableName)
    {
        var value = GetAssignments(content).FirstOrDefault(assignment => assignment.Name == variableName)?.Value;
        var number = value is null ? null : Regex.Match(value, @"^-?(?:\d+(?:\.\d{1,2})?|\.\d{1,2})");
        return number is { Success: true } ? number.Value : null;
    }

    public static string? GetString(string content, string variableName)
    {
        var match = Regex.Match(content, $"(?m)^\\s*{Regex.Escape(variableName)}\\s*=\\s*['\\\"](?<value>[^'\\\"]*)['\\\"]\\r?$");
        return match.Success ? match.Groups["value"].Value : null;
    }

    public static string GetBindingSummary(string content, string weapon)
    {
        var bindings = new[]
        {
            (Suffix: "qq1156777787", Prefix: string.Empty),
            (Suffix: "qq1156777787_second", Prefix: "Alt+"),
            (Suffix: "Third", Prefix: "Ctrl+")
        };
        return string.Join(" · ", bindings
            .Select(binding => new { binding.Prefix, Value = GetNumber(content, $"{weapon}_{binding.Suffix}") })
            .Where(binding => binding.Value is not null && binding.Value is not "0")
            .Select(binding => $"{binding.Prefix}{binding.Value}"));
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

}

public sealed record LuaAssignment(string Name, string Value);
