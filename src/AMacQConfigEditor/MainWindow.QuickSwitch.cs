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
    /// <summary>确认换枪后等待窗口隐藏再去监听按键的待处理目标。</summary>
    private (string Weapon, WeaponBindingSlot Slot)? _pendingBindingCapture;

    private void ShowQuickSwitchWindow()
    {
        // 已在显示中：再次按快捷键只把焦点移回搜索框，保留用户输入的关键词。
        if (_quickSwitchWindow is { } open && open.IsVisible)
        {
            open.FocusSearch();
            return;
        }

        // 已有缓存实例：复用而不是重建。频繁 Ctrl+Alt+M 时每次重建整棵视觉树
        // 会让内存"开一次涨一次"，复用后只有第一次打开付出创建成本。
        if (_quickSwitchWindow is { } cached)
        {
            cached.ShowFresh();
            return;
        }

        var window = new QuickSwitchWindow(this) { Owner = this };
        // 复用实例：窗口隐藏（而非销毁）后字段仍指向它，供下次唤起。
        // 捕获监听只在隐藏后启动一次，所以这里只挂一次 IsVisibleChanged，读取并清空待处理目标。
        window.IsVisibleChanged += (_, args) =>
        {
            if ((bool)args.NewValue) return; // 只关心"从可见变为隐藏"
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var pending = _pendingBindingCapture;
                _pendingBindingCapture = null;
                if (pending is not null) StartBindingCapture(pending.Value.Weapon, pending.Value.Slot);
            }));
        };
        _quickSwitchWindow = window;
        window.ShowFresh();
    }

    IReadOnlyList<string> IWeaponQuickSwitchHost.WeaponNames => _viewModel.Weapons;

    string IWeaponQuickSwitchHost.DisplayNameFor(string weapon) => GetWeaponDisplayName(weapon);

    string IWeaponQuickSwitchHost.BindingSummaryFor(string weapon) => _viewModel.GetBindingSummary(weapon);

    string? IWeaponQuickSwitchHost.CurrentWeaponName => _viewModel.SelectedWeapon;

    void IWeaponQuickSwitchHost.ConfirmWeapon(string weapon, WeaponBindingSlot slot)
    {
        // 复用托盘菜单的选中链路，保证主界面列表、详情区与托盘状态同步刷新，并立即落盘。
        SelectWeaponFromTrayOnUiThread(weapon);
        SaveChanges();

        // 窗口隐藏后开始后台等待按键：这时没有窗口、没有焦点，
        // 用户直接按一下侧键即完成绑定，成功与否用屏幕右上角悬浮提示反馈。
        // 待处理目标由窗口的 Hidden 回调读取并清空，不逐次订阅事件，避免复用实例时累积。
        _pendingBindingCapture = (weapon, slot);
    }

    string IWeaponQuickSwitchHost.BindingValueFor(string weapon, WeaponBindingSlot slot) =>
        _viewModel.GetSlotBindingValue(weapon, slot);

    string? IWeaponQuickSwitchHost.ApplyBinding(string weapon, WeaponBindingSlot slot, string value)
    {
        var conflict = _viewModel.FindBindingConflict(weapon, slot, value);
        var propertyName = slot switch
        {
            WeaponBindingSlot.Alt => nameof(MainWindowViewModel.AltKey),
            WeaponBindingSlot.Ctrl => nameof(MainWindowViewModel.CtrlKey),
            _ => nameof(MainWindowViewModel.PrimaryKey)
        };
        ApplyTrayBinding(weapon, propertyName, value);
        return conflict;
    }

    /// <summary>
    /// 后台等待一次按键用于绑定：侧键监听 + 数字键监听同时生效，任一命中即完成绑定。
    /// 超时（<see cref="BindingCaptureTimeout"/>）没有任何输入就静默结束，不打扰用户。
    /// </summary>
    private void StartBindingCapture(string weapon, WeaponBindingSlot slot) =>
        StartBindingCapture(weapon, slot, DetectHeldSlot);

    /// <summary>
    /// 重载供回归夹具注入固定的"按住修饰键"状态，从而在不依赖真实按键的情况下
    /// 驱动"按住 Alt 再点侧键"这条链路。生产代码走上面的单参重载。
    /// </summary>
    internal void StartBindingCapture(string weapon, WeaponBindingSlot slot, Func<WeaponBindingSlot?> heldSlotProbe)
    {
        CancelBindingCapture();

        _bindingCaptureTarget = (weapon, slot);
        // 侧键与数字键都按"按下那一刻"的修饰键判定槽位：按住 Alt 点侧键就写 Alt 槽位。
        _bindingCaptureMouse = new MouseSideButtonListener(value => HandleCapturedBinding(value, heldSlotProbe()));
        _bindingCaptureKeyboard = new DigitKeyListener(value => HandleCapturedBinding(value, heldSlotProbe()));

        var mouseReady = _bindingCaptureMouse.TryStart();
        var keyboardReady = _bindingCaptureKeyboard.TryStart();
        if (!mouseReady && !keyboardReady)
        {
            CancelBindingCapture();
            _sensitivityOverlay.ShowMessage("无法监听按键，请在托盘菜单中选择绑定", BindingFailureColor);
            return;
        }

        _bindingCaptureTimer.Stop();
        _bindingCaptureTimer.Start();

        // 提示正在等待按键：否则这段时间会静默吞掉侧键与数字键，用户不知道程序在监听。
        _sensitivityOverlay.ShowMessage(
            $"等待按键… 按侧键绑定 {GetWeaponDisplayName(weapon)}",
            BindingWaitingColor,
            BindingWaitingHintDuration);
    }

    private bool HandleCapturedBinding(string value, WeaponBindingSlot? liveSlot)
    {
        // 低层输入钩子安装在本窗口 UI 线程上，正常会直接在这里执行。
        // 必须在钩子回调返回前完成绑定和落盘，随后原始按键才会继续传给 G HUB；
        // 如果只排队到 Dispatcher，按键会先穿过钩子链，读到旧配置。
        if (Dispatcher.CheckAccess())
        {
            return CompleteBindingCapture(value, liveSlot);
        }

        return Dispatcher.Invoke(() => CompleteBindingCapture(value, liveSlot));
    }

    /// <summary>
    /// 接收一次"后台等待按键"的结果并完成绑定。修饰键对应的槽位由调用方给出：
    /// 监听器在按键按下那一刻读取修饰键状态得到，因此按住 Alt 点侧键就写 Alt 槽位；
    /// 没有按住修饰键时传 null，用选枪时选定的槽位。
    /// </summary>
    internal bool CompleteBindingCapture(string value, WeaponBindingSlot? liveSlot)
    {
        if (_bindingCaptureTarget is not { } target) return false;

        var (weapon, preselectedSlot) = target;
        var slot = liveSlot ?? preselectedSlot;
        var conflict = _viewModel.FindBindingConflict(weapon, slot, value);
        var propertyName = slot switch
        {
            WeaponBindingSlot.Alt => nameof(MainWindowViewModel.AltKey),
            WeaponBindingSlot.Ctrl => nameof(MainWindowViewModel.CtrlKey),
            _ => nameof(MainWindowViewModel.PrimaryKey)
        };
        if (!ApplyTrayBinding(weapon, propertyName, value, showSaveError: false))
        {
            CancelBindingCapture();
            _sensitivityOverlay.ShowMessage(
                $"{GetWeaponDisplayName(weapon)} 绑定保存失败，按键未触发",
                BindingFailureColor);
            return false;
        }

        CancelBindingCapture();

        var slotLabel = slot switch
        {
            WeaponBindingSlot.Alt => "Alt",
            WeaponBindingSlot.Ctrl => "Ctrl",
            _ => "主键"
        };
        var message = $"{GetWeaponDisplayName(weapon)} · {slotLabel} 已绑定 {DescribeBindingValue(value)}";
        if (conflict is not null) message += $"（{GetWeaponDisplayName(conflict)} 已清空）";
        _sensitivityOverlay.ShowMessage(message, BindingSuccessColor);
        return true;
    }

    private void CancelBindingCapture()
    {
        _bindingCaptureTimer.Stop();
        _bindingCaptureMouse?.Dispose();
        _bindingCaptureMouse = null;
        _bindingCaptureKeyboard?.Dispose();
        _bindingCaptureKeyboard = null;
        _bindingCaptureTarget = null;
    }

    private static string DescribeBindingValue(string value) => value switch
    {
        "0" => "未绑定",
        "4" => "侧键4",
        "5" => "侧键5",
        _ => $"键值{value}"
    };

    /// <summary>
    /// 读取"此刻"按住的修饰键。用 GetAsyncKeyState 而不是 WPF 的 Keyboard.Modifiers：
    /// 等待期间窗口并不存在，WPF 的键盘状态不可依赖，而全局键状态在任何时候都能读到。
    /// </summary>
    internal static WeaponBindingSlot? DetectHeldSlot()
    {
        if (IsPressed(VirtualKeyControl) || IsPressed(VirtualKeyRControl)) return WeaponBindingSlot.Ctrl;
        if (IsPressed(VirtualKeyMenu) || IsPressed(VirtualKeyRMenu)) return WeaponBindingSlot.Alt;
        return null;
    }


}
