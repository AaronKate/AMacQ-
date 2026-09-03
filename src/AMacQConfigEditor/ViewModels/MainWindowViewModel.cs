using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Globalization;
using AMacQConfigEditor.Models;
using AMacQConfigEditor.Services;

namespace AMacQConfigEditor.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private ConfigurationSession? _session;
    private string? _selectedWeapon;
    private string _statusMessage = "请选择两个 Lua 配置文件。";

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<string> Weapons => _session?.Weapons ?? [];
    public bool CanSave => _session is not null && !string.IsNullOrWhiteSpace(SelectedWeapon);
    public string StatusMessage { get => _statusMessage; private set => Set(ref _statusMessage, value); }

    public string? SelectedWeapon
    {
        get => _selectedWeapon;
        set
        {
            if (Set(ref _selectedWeapon, value))
            {
                LoadSelectedWeaponValues();
                OnPropertyChanged(nameof(CanSave));
            }
        }
    }

    public string PrimaryKey { get; set; } = "0";
    public string AltKey { get; set; } = "0";
    public string CtrlKey { get; set; } = "0";
    public string SensitivityX { get; set; } = "0";
    public string SensitivityY { get; set; } = "0";
    public string SensitivityAddX { get; set; } = "0";
    public string SensitivityAddY { get; set; } = "0";
    public string Press { get; set; } = string.Empty;
    public string ModeSwitch { get; set; } = string.Empty;

    public LoadResult Load(string keyBindingsPath, string sensitivityPath)
    {
        if (string.Equals(keyBindingsPath, sensitivityPath, StringComparison.OrdinalIgnoreCase))
        {
            return LoadResult.Failure("请为两个配置角色选择不同的文件。");
        }
        if (!File.Exists(keyBindingsPath) || !File.Exists(sensitivityPath))
        {
            return LoadResult.Failure("找不到所选的 Lua 配置文件。");
        }

        var keyBindings = FileEncodingService.ReadAllText(keyBindingsPath);
        var sensitivity = FileEncodingService.ReadAllText(sensitivityPath);
        _session = new ConfigurationSession(
            new ConfigFile(keyBindingsPath, keyBindings.Content, keyBindings.Encoding),
            new ConfigFile(sensitivityPath, sensitivity.Content, sensitivity.Encoding));
        Press = LuaConfigService.GetNumber(_session.KeyBindings.Content, "press") ?? "3";
        ModeSwitch = LuaConfigService.GetString(_session.KeyBindings.Content, "modeswitch") ?? "scrolllock";
        OnPropertyChanged(nameof(Press));
        OnPropertyChanged(nameof(ModeSwitch));
        SelectedWeapon = Weapons.FirstOrDefault();
        OnPropertyChanged(nameof(Weapons));
        OnPropertyChanged(nameof(CanSave));
        StatusMessage = Weapons.Count == 0 ? "未找到可编辑的枪械。" : $"已加载 {Weapons.Count} 个枪械配置。";
        return LoadResult.Success();
    }

    public string GetBindingSummary(string weapon) =>
        _session is null ? string.Empty : LuaConfigService.GetBindingSummary(_session.KeyBindings.Content, weapon);

    public string GetBindingValue(string weapon, string suffix) =>
        _session is null ? "0" : LuaConfigService.GetNumber(_session.KeyBindings.Content, $"{weapon}_{suffix}") ?? "0";

    public void RefreshSelectedWeaponValues()
    {
        LoadSelectedWeaponValues();
    }

    public SensitivityAdjustmentResult AdjustCurrentWeaponSensitivity(bool adjustX, int direction)
    {
        if (_session is null || string.IsNullOrWhiteSpace(SelectedWeapon))
            return SensitivityAdjustmentResult.Failure("尚未加载配置或选择枪械。");

        var weapon = SelectedWeapon!;
        var axis = adjustX ? "X" : "Y";
        var baseSuffix = $"qq1156777787_{axis}";
        var baseName = $"{weapon}_{baseSuffix}";
        var baseValue = LuaConfigService.GetNumber(_session.Sensitivity.Content, baseName);
        if (baseValue is null)
            return SensitivityAdjustmentResult.Failure($"当前枪械缺少 {axis} 轴灵敏度配置，未进行修改。");

        var delta = direction > 0 ? 0.05m : -0.05m;
        var newBaseValue = AdjustSensitivityBy(baseValue, delta);
        var updatedContent = LuaConfigService.SetNumber(_session.Sensitivity.Content, baseName, newBaseValue);
        AtomicFileWriter.WriteAllText(_session.Sensitivity.Path, updatedContent, _session.Sensitivity.Encoding);
        _session.Sensitivity.Content = updatedContent;

        if (adjustX)
        {
            SensitivityX = newBaseValue;
            OnPropertyChanged(nameof(SensitivityX));
        }
        else
        {
            SensitivityY = newBaseValue;
            OnPropertyChanged(nameof(SensitivityY));
        }

        StatusMessage = $"{weapon} 的 {axis} 轴已调整。";
        return SensitivityAdjustmentResult.Success(weapon, axis, newBaseValue);
    }

    public void Save()
    {
        if (_session is null || string.IsNullOrWhiteSpace(SelectedWeapon)) return;
        ValidateKey(PrimaryKey); ValidateKey(AltKey); ValidateKey(CtrlKey);
        ValidateDecimal(SensitivityX); ValidateDecimal(SensitivityY); ValidateDecimal(SensitivityAddX); ValidateDecimal(SensitivityAddY);
        _session.KeyBindings.Content = LuaConfigService.SetNumber(_session.KeyBindings.Content, $"{SelectedWeapon}_qq1156777787", PrimaryKey);
        _session.KeyBindings.Content = LuaConfigService.ClearConflictingBinding(_session.KeyBindings.Content, SelectedWeapon!, "qq1156777787", PrimaryKey);
        _session.KeyBindings.Content = LuaConfigService.SetNumber(_session.KeyBindings.Content, "press", Press);
        _session.KeyBindings.Content = LuaConfigService.SetString(_session.KeyBindings.Content, "modeswitch", ModeSwitch);
        _session.KeyBindings.Content = LuaConfigService.SetNumber(_session.KeyBindings.Content, $"{SelectedWeapon}_qq1156777787_second", AltKey);
        _session.KeyBindings.Content = LuaConfigService.ClearConflictingBinding(_session.KeyBindings.Content, SelectedWeapon!, "qq1156777787_second", AltKey);
        _session.KeyBindings.Content = LuaConfigService.SetNumber(_session.KeyBindings.Content, $"{SelectedWeapon}_Third", CtrlKey);
        _session.KeyBindings.Content = LuaConfigService.ClearConflictingBinding(_session.KeyBindings.Content, SelectedWeapon!, "Third", CtrlKey);
        _session.Sensitivity.Content = LuaConfigService.SetNumber(_session.Sensitivity.Content, $"{SelectedWeapon}_qq1156777787_X", SensitivityX);
        _session.Sensitivity.Content = LuaConfigService.SetNumber(_session.Sensitivity.Content, $"{SelectedWeapon}_qq1156777787_Y", SensitivityY);
        _session.Sensitivity.Content = LuaConfigService.SetNumber(_session.Sensitivity.Content, $"{SelectedWeapon}_qq1156777787_add_X", SensitivityAddX);
        _session.Sensitivity.Content = LuaConfigService.SetNumber(_session.Sensitivity.Content, $"{SelectedWeapon}_qq1156777787_add_Y", SensitivityAddY);
        AtomicFileWriter.WriteAllText(_session.KeyBindings.Path, _session.KeyBindings.Content, _session.KeyBindings.Encoding);
        AtomicFileWriter.WriteAllText(_session.Sensitivity.Path, _session.Sensitivity.Content, _session.Sensitivity.Encoding);
        StatusMessage = "应用成功。";
    }

    private void LoadSelectedWeaponValues()
    {
        if (_session is null || string.IsNullOrWhiteSpace(SelectedWeapon)) return;
        PrimaryKey = Value(_session.KeyBindings.Content, "qq1156777787"); AltKey = Value(_session.KeyBindings.Content, "qq1156777787_second"); CtrlKey = Value(_session.KeyBindings.Content, "Third");
        SensitivityX = Value(_session.Sensitivity.Content, "qq1156777787_X"); SensitivityY = Value(_session.Sensitivity.Content, "qq1156777787_Y");
        SensitivityAddX = Value(_session.Sensitivity.Content, "qq1156777787_add_X"); SensitivityAddY = Value(_session.Sensitivity.Content, "qq1156777787_add_Y");
        foreach (var property in new[] { nameof(PrimaryKey), nameof(AltKey), nameof(CtrlKey), nameof(SensitivityX), nameof(SensitivityY), nameof(SensitivityAddX), nameof(SensitivityAddY) }) OnPropertyChanged(property);
    }

    private string Value(string content, string suffix) => LuaConfigService.GetNumber(content, $"{SelectedWeapon}_{suffix}") ?? "0";
    private static void ValidateKey(string value) { if (!Regex.IsMatch(value, "^[0-9]$")) throw new InvalidOperationException("按键值必须为 0 到 9。"); }
    public static bool IsValidSensitivityValue(string value) => Regex.IsMatch(value, "^\\d+(?:\\.\\d{1,2})?$");
    public static string AdjustSensitivityValue(string value, int direction)
    {
        if (!decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number)) number = 0m;
        number = Math.Max(0m, Math.Round(number + (direction > 0 ? 0.01m : -0.01m), 2));
        return number.ToString("0.##", CultureInfo.InvariantCulture);
    }
    private static void ValidateDecimal(string value) { if (!IsValidSensitivityValue(value)) throw new InvalidOperationException("灵敏度必须是非负整数或最多两位小数。"); }
    private static string AdjustSensitivityBy(string value, decimal delta)
    {
        if (!decimal.TryParse(value, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number))
            throw new InvalidOperationException("灵敏度配置格式无效。");
        number = Math.Max(0m, Math.Round(number + delta, 2));
        return number.ToString("0.##", CultureInfo.InvariantCulture);
    }
    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null) { if (EqualityComparer<T>.Default.Equals(field, value)) return false; field = value; OnPropertyChanged(name); return true; }
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed record SensitivityAdjustmentResult(bool IsSuccess, string? Error, string? Weapon, string? Axis, string? BaseValue)
{
    public static SensitivityAdjustmentResult Success(string weapon, string axis, string baseValue) => new(true, null, weapon, axis, baseValue);
    public static SensitivityAdjustmentResult Failure(string error) => new(false, error, null, null, null);
}

public sealed record LoadResult(bool IsSuccess, string? Error)
{
    public static LoadResult Success() => new(true, null);
    public static LoadResult Failure(string error) => new(false, error);
}
