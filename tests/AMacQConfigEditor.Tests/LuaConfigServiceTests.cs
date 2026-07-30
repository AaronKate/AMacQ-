using AMacQConfigEditor.Services;
using AMacQConfigEditor.ViewModels;
using Xunit;

namespace AMacQConfigEditor.Tests;

public sealed class LuaConfigServiceTests
{
    [Fact]
    public void SetNumber_changes_only_the_exact_variable()
    {
        const string source = "ak_x = 1\nak_xy = 2\n";

        var result = LuaConfigService.SetNumber(source, "ak_x", "9");

        Assert.Equal("ak_x = 9\nak_xy = 2\n", result);
    }

    [Fact]
    public void SetString_keeps_the_existing_quote_style()
    {
        var result = LuaConfigService.SetString("modeswitch = 'g'", "modeswitch", "f");

        Assert.Equal("modeswitch = 'f'", result);
    }

    [Fact]
    public void ClearConflictingBindings_resets_matching_nonzero_binding_on_another_weapon()
    {
        const string source = "ak_key = 4\nm4_key = 4\nm4_alt = 2\n";

        var result = LuaConfigService.ClearConflictingBindings(source, "ak", new Dictionary<string, string> { ["key"] = "4" });

        Assert.Equal("ak_key = 4\nm4_key = 0\nm4_alt = 2\n", result);
    }

    [Fact]
    public void GetPrimaryWeapons_parses_crlf_lua_files()
    {
        const string source = "AKM_qq1156777787 = 0\r\nM4A1_qq1156777787_second = 4\r\n";

        var weapons = LuaConfigService.GetPrimaryWeapons(source);

        Assert.Equal(["AKM", "M4A1"], weapons);
    }

    [Fact]
    public void GetNumber_ignores_a_trailing_lua_comment()
    {
        Assert.Equal("3", LuaConfigService.GetNumber("press=3  ----quickfire 1 3\r\n", "press"));
    }

    [Fact]
    public void GetBindingSummary_formats_the_three_supported_modifiers()
    {
        const string source = "AKM_qq1156777787=4\r\nAKM_qq1156777787_second=5\r\nAKM_Third=7\r\n";

        Assert.Equal("4 · Alt+5 · Ctrl+7", LuaConfigService.GetBindingSummary(source, "AKM"));
    }

    [Fact]
    public void SetNumber_preserves_trailing_lua_comments()
    {
        const string source = "SVCH_qq1156777787=0  --SVCH\r\n";

        Assert.Equal("SVCH_qq1156777787=4  --SVCH\r\n", LuaConfigService.SetNumber(source, "SVCH_qq1156777787", "4"));
    }

    [Theory]
    [InlineData("0", true)]
    [InlineData("1", true)]
    [InlineData("1.2", true)]
    [InlineData("1.25", true)]
    [InlineData("-1", false)]
    [InlineData("1.234", false)]
    [InlineData("abc", false)]
    public void IsValidSensitivityValue_allows_only_nonnegative_values_with_at_most_two_decimals(string value, bool expected)
    {
        Assert.Equal(expected, MainWindowViewModel.IsValidSensitivityValue(value));
    }

    [Theory]
    [InlineData("1.2", 1, "1.21")]
    [InlineData("1.2", -1, "1.19")]
    [InlineData("0", -1, "0")]
    public void AdjustSensitivityValue_changes_increments_of_point_zero_one_without_going_below_zero(string value, int direction, string expected)
    {
        Assert.Equal(expected, MainWindowViewModel.AdjustSensitivityValue(value, direction));
    }
}
