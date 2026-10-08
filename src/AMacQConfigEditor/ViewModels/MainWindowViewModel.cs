using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;
using AMacQConfigEditor.Models;
using AMacQConfigEditor.Services;

namespace AMacQConfigEditor.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private ConfigurationSession? _session;
    private string? _selectedWeapon;
    private string _statusMessage = "请选择两个 Lua 配置文件。";
    private readonly object _sensitivityWriteLock = new();
    private CancellationTokenSource? _sensitivityWriteCancellation;
    private const int SensitivityWriteDelayMilliseconds = 2000;

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
        FlushPendingSensitivityWrite();
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
        Press = _session.KeyBindingsDocument.GetNumber("press") ?? "3";
        ModeSwitch = _session.KeyBindingsDocument.GetString("modeswitch") ?? "scrolllock";
        OnPropertyChanged(nameof(Press));
        OnPropertyChanged(nameof(ModeSwitch));
        SelectedWeapon = Weapons.FirstOrDefault();
        OnPropertyChanged(nameof(Weapons));
        OnPropertyChanged(nameof(CanSave));
        StatusMessage = Weapons.Count == 0 ? "未找到可编辑的枪械。" : $"已加载 {Weapons.Count} 个枪械配置。";
        return LoadResult.Success();
    }

    public string GetBindingSummary(string weapon) =>
        _session is null ? string.Empty : _session.KeyBindingsDocument.GetBindingSummary(weapon);

    public string GetBindingValue(string weapon, string suffix) =>
        _session?.KeyBindingsDocument.GetNumber($"{weapon}_{suffix}") ?? "0";

    public string GetSensitivityValue(string weapon, string suffix) =>
        _session?.SensitivityDocument.GetNumber($"{weapon}_{suffix}") ?? "0";

    /// <summary>枪械三条按键槽位在配置里对应的变量后缀。</summary>
    public static string SuffixFor(WeaponBindingSlot slot) => slot switch
    {
        WeaponBindingSlot.Alt => "qq1156777787_second",
        WeaponBindingSlot.Ctrl => "Third",
        _ => "qq1156777787"
    };

    /// <summary>读取指定枪械某槽位的按键值，"0" 表示未绑定。</summary>
    public string GetSlotBindingValue(string weapon, WeaponBindingSlot slot) =>
        _session?.KeyBindingsDocument.GetNumber($"{weapon}_{SuffixFor(slot)}") ?? "0";

    /// <summary>
    /// 找出"同一槽位已被其它枪械占用"的枪名。用于在写入前提示用户绑定会被顶掉，
    /// 而不是事后发现另一把枪进游戏没反应。写入值为 0 时不存在冲突。
    /// </summary>
    public string? FindBindingConflict(string weapon, WeaponBindingSlot slot, string value)
    {
        if (_session is null || value is "0" || string.IsNullOrWhiteSpace(value)) return null;

        var suffix = SuffixFor(slot);
        foreach (var name in _session.Weapons)
        {
            if (string.Equals(name, weapon, StringComparison.Ordinal)) continue;
            if (_session.KeyBindingsDocument.GetNumber($"{name}_{suffix}") == value) return name;
        }

        return null;
    }

    public void RefreshSelectedWeaponValues()
    {
        LoadSelectedWeaponValues();
    }

    public SensitivityAdjustmentResult AdjustCurrentWeaponSensitivity(bool adjustX, int direction, decimal step = 0.01m)
    {
        if (_session is null || string.IsNullOrWhiteSpace(SelectedWeapon))
            return SensitivityAdjustmentResult.Failure("尚未加载配置或选择枪械。");

        var weapon = SelectedWeapon!;
        var axis = adjustX ? "X" : "Y";
        var baseSuffix = $"qq1156777787_{axis}";
        var baseName = $"{weapon}_{baseSuffix}";
        var baseValue = _session.SensitivityDocument.GetNumber(baseName);
        if (baseValue is null)
            return SensitivityAdjustmentResult.Failure($"当前枪械缺少 {axis} 轴灵敏度配置，未进行修改。");

        var delta = direction > 0 ? step : -step;
        var newBaseValue = AdjustSensitivityBy(baseValue, delta);
        _session.SensitivityDocument.SetNumber(baseName, newBaseValue);
        _session.Sensitivity.Content = _session.SensitivityDocument.ApplyPendingChanges();
        ScheduleSensitivityWrite();

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

    private void ScheduleSensitivityWrite()
    {
        CancellationToken token;
        lock (_sensitivityWriteLock)
        {
            // 先取消再释放：连续微调时每次都会新建 CTS，不释放会持续累积。
            _sensitivityWriteCancellation?.Cancel();
            _sensitivityWriteCancellation?.Dispose();
            _sensitivityWriteCancellation = new CancellationTokenSource();
            token = _sensitivityWriteCancellation.Token;
        }

        _ = WriteSensitivityAfterDelayAsync(token);
    }

    private async Task WriteSensitivityAfterDelayAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(SensitivityWriteDelayMilliseconds, token).ConfigureAwait(false);
            lock (_sensitivityWriteLock)
            {
                if (token.IsCancellationRequested || _session is null) return;
                AtomicFileWriter.WriteAllText(_session.Sensitivity.Path, _session.Sensitivity.Content, _session.Sensitivity.Encoding);
            }
        }
        catch (OperationCanceledException)
        {
            // 用户继续调整时取消本次延迟写入。
        }
        catch (IOException)
        {
            // 保留内存中的最新值，后续调整或退出时会再次尝试写入。
        }
        catch (UnauthorizedAccessException)
        {
            // 保留内存中的最新值，后续调整或退出时会再次尝试写入。
        }
    }

    public void FlushPendingSensitivityWrite()
    {
        lock (_sensitivityWriteLock)
        {
            _sensitivityWriteCancellation?.Cancel();
            _sensitivityWriteCancellation?.Dispose();
            _sensitivityWriteCancellation = null;
            if (_session is null) return;
            AtomicFileWriter.WriteAllText(_session.Sensitivity.Path, _session.Sensitivity.Content, _session.Sensitivity.Encoding);
        }
    }

    public void Save()
    {
        if (_session is null || string.IsNullOrWhiteSpace(SelectedWeapon)) return;

        // 写盘前校验：宁可在这里给出明确提示，也不要把非法值（尤其是空值）写进用户的配置文件。
        ValidateKey(PrimaryKey, "无修饰键");
        ValidateKey(AltKey, "按住 Alt");
        ValidateKey(CtrlKey, "按住 Ctrl");
        ValidateDecimal(SensitivityX, "灵敏度 X");
        ValidateDecimal(SensitivityY, "灵敏度 Y");
        ValidateDecimal(SensitivityAddX, "灵敏度 增幅 X");
        ValidateDecimal(SensitivityAddY, "灵敏度 增幅 Y");

        ApplySelectedWeaponValues();
        FlushPendingSensitivityWrite();
        StatusMessage = "应用成功。";
    }

    private static void ValidateKey(string? value, string label)
    {
        if (value is null || !Regex.IsMatch(value, "^[0-9]$"))
            throw new InvalidOperationException($"「{label}」的按键值无效（当前为 {value ?? "空"}），请重新选择后再应用。");
    }

    private static void ValidateDecimal(string? value, string label)
    {
        if (value is null || !IsValidSensitivityValue(value))
            throw new InvalidOperationException($"「{label}」的数值无效（当前为 {value ?? "空"}），请重新填写后再应用。");
    }

    /// <summary>
    /// 把当前编辑框里的值写回内存中的配置文档并落盘。文档的写入缓冲会在一批修改后
    /// 统一应用，因此这里只触发一次全文写回，避免逐字段写回的重复开销。
    /// </summary>
    private void ApplySelectedWeaponValues()
    {
        if (_session is null || string.IsNullOrWhiteSpace(SelectedWeapon)) return;

        var keyBindings = _session.KeyBindingsDocument;
        var sensitivity = _session.SensitivityDocument;
        keyBindings.SetNumber($"{SelectedWeapon}_qq1156777787", PrimaryKey);
        keyBindings.ClearConflictingBinding(SelectedWeapon!, "qq1156777787", PrimaryKey);
        keyBindings.SetNumber("press", Press);
        keyBindings.SetString("modeswitch", ModeSwitch);
        keyBindings.SetNumber($"{SelectedWeapon}_qq1156777787_second", AltKey);
        keyBindings.ClearConflictingBinding(SelectedWeapon!, "qq1156777787_second", AltKey);
        keyBindings.SetNumber($"{SelectedWeapon}_Third", CtrlKey);
        keyBindings.ClearConflictingBinding(SelectedWeapon!, "Third", CtrlKey);
        sensitivity.SetNumber($"{SelectedWeapon}_qq1156777787_X", SensitivityX);
        sensitivity.SetNumber($"{SelectedWeapon}_qq1156777787_Y", SensitivityY);
        sensitivity.SetNumber($"{SelectedWeapon}_qq1156777787_add_X", SensitivityAddX);
        sensitivity.SetNumber($"{SelectedWeapon}_qq1156777787_add_Y", SensitivityAddY);
        _session.KeyBindings.Content = keyBindings.ApplyPendingChanges();
        _session.Sensitivity.Content = sensitivity.ApplyPendingChanges();
        AtomicFileWriter.WriteAllText(_session.KeyBindings.Path, _session.KeyBindings.Content, _session.KeyBindings.Encoding);
    }

    private void LoadSelectedWeaponValues()
    {
        if (_session is null || string.IsNullOrWhiteSpace(SelectedWeapon)) return;
        PrimaryKey = Value(_session.KeyBindingsDocument, "qq1156777787"); AltKey = Value(_session.KeyBindingsDocument, "qq1156777787_second"); CtrlKey = Value(_session.KeyBindingsDocument, "Third");
        SensitivityX = Value(_session.SensitivityDocument, "qq1156777787_X"); SensitivityY = Value(_session.SensitivityDocument, "qq1156777787_Y");
        SensitivityAddX = Value(_session.SensitivityDocument, "qq1156777787_add_X"); SensitivityAddY = Value(_session.SensitivityDocument, "qq1156777787_add_Y");
        foreach (var property in new[] { nameof(PrimaryKey), nameof(AltKey), nameof(CtrlKey), nameof(SensitivityX), nameof(SensitivityY), nameof(SensitivityAddX), nameof(SensitivityAddY) }) OnPropertyChanged(property);
    }

    private string Value(LuaConfigService.LuaDocument document, string suffix) => document.GetNumber($"{SelectedWeapon}_{suffix}") ?? "0";
    public static bool IsValidSensitivityValue(string value) => Regex.IsMatch(value, "^\\d+(?:\\.\\d{1,2})?$");
    public static string AdjustSensitivityValue(string value, int direction)
    {
        if (!decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number)) number = 0m;
        number = Math.Max(0m, Math.Round(number + (direction > 0 ? 0.01m : -0.01m), 2));
        return number.ToString("0.##", CultureInfo.InvariantCulture);
    }
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

