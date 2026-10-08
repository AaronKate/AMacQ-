using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using AMacQConfigEditor.Licensing;
using AMacQConfigEditor.Services;
using AMacQConfigEditor.ViewModels;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace AMacQConfigEditor;

public partial class MainWindow
{
    private void InitializeGlobalHotKeys()
    {
        _windowHandle = new WindowInteropHelper(this).Handle;
        _windowSource = HwndSource.FromHwnd(_windowHandle);
        _windowSource?.AddHook(HandleWindowMessage);

        RegisterGlobalHotKey(HotKeyXDecrease, 0x25, "Ctrl + Alt + 左方向键");
        RegisterGlobalHotKey(HotKeyXIncrease, 0x27, "Ctrl + Alt + 右方向键");
        RegisterGlobalHotKey(HotKeyYDecrease, 0x28, "Ctrl + Alt + 下方向键");
        RegisterGlobalHotKey(HotKeyYIncrease, 0x26, "Ctrl + Alt + 上方向键");
        RegisterGlobalHotKey(HotKeyWeaponMenu, 0x4D, "Ctrl + Alt + M");
        RegisterGlobalHotKey(HotKeyYFastDecrease, 0xBD, "Ctrl + Alt + -");
        RegisterGlobalHotKey(HotKeyYFastIncrease, 0xBB, "Ctrl + Alt + =");
    }

    private static bool IsPressed(int virtualKey) => (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    private static bool IsControlAltModifierCombo() =>
        IsPressed(VkControl) && IsPressed(VkMenu);

    private void RegisterGlobalHotKey(int id, uint virtualKey, string shortcut)
    {
        if (RegisterHotKey(_windowHandle, id, ModControl | ModAlt | ModNoRepeat, virtualKey))
        {
            _registeredHotKeys.Add(id);
            return;
        }

        _trayIcon.ShowBalloonTip(3000, "AMacQ", $"快捷键 {shortcut} 注册失败，可能已被其他程序占用。", Forms.ToolTipIcon.Warning);
    }

    private IntPtr HandleWindowMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message != WmHotKey) return IntPtr.Zero;

        var id = wParam.ToInt32();
        if (!_registeredHotKeys.Contains(id)) return IntPtr.Zero;

        handled = true;
        if (!IsControlAltModifierCombo()) return IntPtr.Zero;

        if (id is HotKeyYFastDecrease or HotKeyYFastIncrease)
        {
            ApplyHotKeyAdjustment(false, id == HotKeyYFastIncrease ? 1 : -1, 0.05m);
            return IntPtr.Zero;
        }

        var adjustment = id switch
        {
            HotKeyXDecrease => (AdjustX: true, Direction: -1),
            HotKeyXIncrease => (AdjustX: true, Direction: 1),
            HotKeyYDecrease => (AdjustX: false, Direction: -1),
            HotKeyYIncrease => (AdjustX: false, Direction: 1),
            _ => (AdjustX: true, Direction: 0)
        };
        if (id == HotKeyWeaponMenu)
        {
            ShowQuickSwitchWindow();
            return IntPtr.Zero;
        }

        if (adjustment.Direction != 0) ApplyHotKeyAdjustment(adjustment.AdjustX, adjustment.Direction);
        return IntPtr.Zero;
    }

    /// <summary>
    /// Ctrl+Alt+M 唤起纯键盘快速切换窗口：输入筛选 + ↑/↓ + Enter，全程不需要鼠标。
    /// 窗口已打开时再次按快捷键只把焦点移回搜索框。
    /// </summary>

    private void ApplyHotKeyAdjustment(bool adjustX, int direction, decimal step = 0.01m)
    {
        var result = _viewModel.AdjustCurrentWeaponSensitivity(adjustX, direction, step);
        if (!result.IsSuccess)
        {
            QueueHotKeyNotification(result.Error ?? "当前枪械灵敏度调整失败。", Forms.ToolTipIcon.Warning);
            return;
        }

        // 托盘菜单第一层的灵敏度状态会实时刷新，无需在每次微调后重建整棵枪械菜单。
        ShowSensitivityOverlay();
    }

    private void QueueHotKeyNotification(string message, Forms.ToolTipIcon icon)
    {
        _pendingHotKeyNotification = message;
        _pendingHotKeyNotificationIcon = icon;
        _hotKeyNotificationTimer.Stop();
        _hotKeyNotificationTimer.Start();
    }

    private void FlushHotKeyNotification()
    {
        _hotKeyNotificationTimer.Stop();
        if (string.IsNullOrWhiteSpace(_pendingHotKeyNotification)) return;

        _trayIcon.ShowBalloonTip(2000, "AMacQ", _pendingHotKeyNotification, _pendingHotKeyNotificationIcon);
        _pendingHotKeyNotification = null;
    }

    private void UnregisterGlobalHotKeys()
    {
        if (_windowHandle == IntPtr.Zero) return;
        foreach (var id in _registeredHotKeys) UnregisterHotKey(_windowHandle, id);
        _registeredHotKeys.Clear();
        _windowSource?.RemoveHook(HandleWindowMessage);
        _windowSource = null;
        _windowHandle = IntPtr.Zero;
    }

    private void HideToTray()
    {
        Hide();
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }


    private void ShowSensitivityOverlay()
    {
        if (string.IsNullOrWhiteSpace(_viewModel.SelectedWeapon)) return;
        _sensitivityOverlay.ShowWeaponSensitivity(
            GetWeaponDisplayName(_viewModel.SelectedWeapon),
            _viewModel.SensitivityX,
            _viewModel.SensitivityY);
    }


}
