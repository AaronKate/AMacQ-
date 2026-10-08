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
    private void ConfigureTrayIcon()
    {
        using var iconStream = typeof(MainWindow).Assembly.GetManifestResourceStream("AMacQConfigEditor.Resources.AMacQ.ico");
        if (iconStream is null)
        {
            _trayIcon.Icon = Drawing.SystemIcons.Application;
        }
        else
        {
            using var icon = new Drawing.Icon(iconStream);
            _trayIcon.Icon = (Drawing.Icon)icon.Clone();
        }
        _trayIcon.Text = "AMacQ Configuration Editor";
        _trayMenu.Renderer = new TrayMenuRenderer(this);
        ApplyTrayMenuCornerRadius(_trayMenu);
        _trayMenu.ShowImageMargin = false;
        _trayMenu.ShowCheckMargin = true;
        _trayMenu.Font = new Drawing.Font("Segoe UI", 11F);
        _trayMenu.ItemAdded += (_, args) => StyleTrayMenuItem(args.Item);
        _trayMenu.Items.Add("打开主窗口", null, (_, _) =>
        {
            _trayMenu.Close();
            RestoreFromTray();
        });
        _trayCurrentWeaponStatus = new Forms.ToolStripMenuItem { Enabled = false };
        _trayMenu.Items.Add(_trayCurrentWeaponStatus);
        _traySensitivityStatus = new Forms.ToolStripMenuItem
        {
            DropDownDirection = TraySubMenuDirection,
            ToolTipText = "点击展开，每次调整 0.05 并自动保存"
        };
        ConfigureTrayDropDown(_traySensitivityStatus.DropDown);
        _traySensitivityStatus.DropDownItems.Add(CreateTraySensitivityMenuItem("X −0.05", true, -1, 0.05m));
        _traySensitivityStatus.DropDownItems.Add(CreateTraySensitivityMenuItem("X +0.05", true, 1, 0.05m));
        _traySensitivityStatus.DropDownItems.Add(new Forms.ToolStripSeparator());
        _traySensitivityStatus.DropDownItems.Add(CreateTraySensitivityMenuItem("Y −0.05", false, -1, 0.05m));
        _traySensitivityStatus.DropDownItems.Add(CreateTraySensitivityMenuItem("Y +0.05", false, 1, 0.05m));
        _trayMenu.Items.Add(_traySensitivityStatus);
        _trayMenu.Items.Add(new Forms.ToolStripSeparator());
        _trayMenu.Items.Add("退出", null, (_, _) => Close());
        _trayIcon.ContextMenuStrip = _trayMenu;
        _trayIcon.DoubleClick += (_, _) => RestoreFromTray();
        _trayMenu.Opening += (_, _) =>
        {
            RefreshTrayWeaponMenuIfNeeded();
            StartTrayOutsideClickWatcher();
        };
        _trayMenu.Closed += (_, _) => StopTrayOutsideClickWatcher();
        Closed += (_, _) => StopTrayOutsideClickWatcher();
        _trayMenu.Closing += HandleTrayMenuClosing;
        _trayIcon.Visible = true;
    }


    private static void StyleTrayMenuItem(Forms.ToolStripItem item)
    {
        if (item is Forms.ToolStripMenuItem menuItem)
        {
            menuItem.Padding = new Forms.Padding(10, 5, 10, 5);
        }
    }

    private void ConfigureTrayDropDown(Forms.ToolStripDropDown dropDown)
    {
        // 同一实例只配置一次，避免每次重建都对常驻下拉菜单重复订阅 ItemAdded 等事件。
        if (!_configuredTrayMenus.Add(dropDown)) return;

        dropDown.Renderer = new TrayMenuRenderer(this);
        dropDown.Font = new Drawing.Font("Segoe UI", 11F);
        dropDown.ItemAdded += (_, args) => StyleTrayMenuItem(args.Item);
        dropDown.Closing += HandleTrayMenuClosing;
        if (dropDown is Forms.ToolStripDropDownMenu menu)
        {
            menu.ShowImageMargin = false;
            menu.ShowCheckMargin = true;
            menu.AutoSize = true;
        }
        ApplyTrayMenuCornerRadius(dropDown);
    }

    private void ApplyTrayMenuCornerRadius(Forms.ToolStripDropDown dropDown)
    {
        if (!_roundedTrayMenus.Add(dropDown)) return;

        dropDown.SizeChanged += (_, _) => UpdateTrayMenuRegion(dropDown);
        dropDown.Opened += (_, _) => UpdateTrayMenuRegion(dropDown);
        dropDown.Disposed += (_, _) =>
        {
            _roundedTrayMenus.Remove(dropDown);
            _configuredTrayMenus.Remove(dropDown);
        };
    }

    private static void UpdateTrayMenuRegion(Forms.ToolStripDropDown dropDown)
    {
        if (dropDown.Width <= 0 || dropDown.Height <= 0) return;
        using var path = CreateRoundedRectanglePath(new Drawing.Rectangle(0, 0, dropDown.Width, dropDown.Height), TrayMenuCornerRadius);
        dropDown.Region = new Drawing.Region(path);
    }

    private static Drawing.Drawing2D.GraphicsPath CreateRoundedRectanglePath(Drawing.Rectangle bounds, int radius)
    {
        var path = new Drawing.Drawing2D.GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private void RefreshTrayWeaponMenuIfNeeded()
    {
        UpdateTrayCurrentWeaponStatus();
        if (!_trayMenuDirty)
        {
            // 菜单结构没变，但当前枪械可能变了：只刷新勾选，不重建整棵树。
            UpdateTrayWeaponCheckMarks();
            return;
        }

        _trayMenuDirty = false;
        RefreshTrayWeaponMenu();
    }

    private void MarkTrayWeaponMenuDirty() => _trayMenuDirty = true;

    private void RefreshTrayWeaponMenu()
    {
        UpdateTrayCurrentWeaponStatus();

        // 重建前先递归释放整棵旧菜单树：若只 Clear 而不 Dispose，被换掉的菜单项及其
        // 子 DropDown、字体、渲染器会因 _roundedTrayMenus 等集合的强引用而无法回收，
        // 每切换一次枪械或调整一次灵敏度都会遗留一批，导致内存持续增长。
        foreach (var item in _trayWeaponMenuItems)
        {
            if (item is Forms.ToolStripDropDownItem { DropDownItems.Count: > 0 } dropDownItem)
            {
                DisposeMenuItems(dropDownItem.DropDownItems);
            }
            item.Dispose();
        }
        _trayWeaponMenuItems.Clear();

        if (_viewModel.Weapons.Count == 0)
        {
            AddTrayWeaponMenuItem(new Forms.ToolStripMenuItem("尚未加载配置") { Enabled = false });
            return;
        }

        var categoryOrder = new[] { "突击步枪", "冲锋枪", "轻机枪", "射手步枪", "狙击步枪", "霰弹枪", "手枪", "其他" };
        var groupedWeapons = _viewModel.Weapons
            .GroupBy(GetWeaponCategory)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);

        foreach (var category in categoryOrder)
        {
            if (!groupedWeapons.TryGetValue(category, out var weapons) || weapons.Length == 0) continue;

            var categoryMenu = new Forms.ToolStripMenuItem(category);
            categoryMenu.DropDownDirection = TraySubMenuDirection;
            ConfigureTrayDropDown(categoryMenu.DropDown);
            foreach (var weapon in weapons)
            {
                var weaponMenu = new Forms.ToolStripMenuItem(GetWeaponDisplayName(weapon))
                {
                    DropDownDirection = TraySubMenuDirection,
                    Tag = weapon
                };
                ConfigureTrayDropDown(weaponMenu.DropDown);
                weaponMenu.DropDownItems.Add(new Forms.ToolStripMenuItem("仅选择此枪械", null, (_, _) =>
                {
                    SelectWeaponFromTray(weapon);
                    _trayMenu.Close();
                }));
                weaponMenu.DropDownItems.Add(new Forms.ToolStripSeparator());

                // 按键位子菜单延迟到该枪械子菜单展开时才构建：36 把枪 × 3 个槽位 × 若干选项
                // 全量预建会产生 500+ 个菜单项，实测让菜单出现要等约 250ms。
                weaponMenu.DropDownOpening += (sender, _) => PopulateTrayBindingMenus((Forms.ToolStripMenuItem)sender!, weapon);
                categoryMenu.DropDownItems.Add(weaponMenu);
            }
            AddTrayWeaponMenuItem(categoryMenu);
        }

        UpdateTrayWeaponCheckMarks();
    }

    /// <summary>
    /// 展开某把枪械时才构建它的三个按键位子菜单，并在每次展开时重建，保证显示的是最新绑定。
    /// </summary>
    private void PopulateTrayBindingMenus(Forms.ToolStripMenuItem weaponMenu, string weapon)
    {
        // 只移除上一轮的按键位子菜单，保留"仅选择此枪械"与分隔线（索引 0、1）。
        while (weaponMenu.DropDownItems.Count > 2)
        {
            var stale = weaponMenu.DropDownItems[2];
            weaponMenu.DropDownItems.RemoveAt(2);
            if (stale is Forms.ToolStripDropDownItem { DropDownItems.Count: > 0 } staleDropDown)
            {
                DisposeMenuItems(staleDropDown.DropDownItems);
            }
            stale.Dispose();
        }

        AddTrayBindingMenu(weaponMenu, weapon, "无修饰键", "qq1156777787", nameof(MainWindowViewModel.PrimaryKey));
        AddTrayBindingMenu(weaponMenu, weapon, "按住 Alt", "qq1156777787_second", nameof(MainWindowViewModel.AltKey));
        AddTrayBindingMenu(weaponMenu, weapon, "按住 Ctrl", "Third", nameof(MainWindowViewModel.CtrlKey));
    }

    /// <summary>刷新各枪械项的勾选状态（标记当前枪械）。只改属性，代价很低。</summary>
    private void UpdateTrayWeaponCheckMarks()
    {
        foreach (var item in _trayWeaponMenuItems)
        {
            if (item is not Forms.ToolStripMenuItem categoryMenu) continue;
            foreach (Forms.ToolStripItem child in categoryMenu.DropDownItems)
            {
                if (child is not Forms.ToolStripMenuItem weaponItem || weaponItem.Tag is not string weapon) continue;
                weaponItem.Checked = string.Equals(_viewModel.SelectedWeapon, weapon, StringComparison.Ordinal);
            }
        }
    }

    private void AddTrayWeaponMenuItem(Forms.ToolStripItem item)
    {
        var separatorIndex = _trayMenu.Items
            .Cast<Forms.ToolStripItem>()
            .ToList()
            .FindIndex(candidate => candidate is Forms.ToolStripSeparator);
        if (separatorIndex < 0) separatorIndex = _trayMenu.Items.Count;

        _trayMenu.Items.Insert(separatorIndex, item);
        _trayWeaponMenuItems.Add(item);
    }

    private Forms.ToolStripMenuItem CreateTraySensitivityMenuItem(string text, bool adjustX, int direction, decimal step)
    {
        var item = new Forms.ToolStripMenuItem(text, null, (_, _) => KeepTrayMenuOpenForSensitivityAdjustment(adjustX, direction, step));
        // WinForms raises the menu's Closing event before the item's Click callback.
        // Arm the guard on MouseDown so the automatic ItemClicked close can be cancelled.
        item.MouseDown += (_, _) => _keepTrayMenuOpenForSensitivity = true;
        item.MouseUp += (_, _) => ResetTraySensitivityMenuGuard();
        return item;
    }

    private void KeepTrayMenuOpenForSensitivityAdjustment(bool adjustX, int direction, decimal step)
    {
        _keepTrayMenuOpenForSensitivity = true;
        AdjustTraySensitivity(adjustX, direction, step);
        ResetTraySensitivityMenuGuard();
    }

    private void ResetTraySensitivityMenuGuard()
    {
        Dispatcher.BeginInvoke(new Action(() => _keepTrayMenuOpenForSensitivity = false), DispatcherPriority.Background);
    }

    private void AdjustTraySensitivity(bool adjustX, int direction, decimal step)
    {
        if (string.IsNullOrWhiteSpace(_viewModel.SelectedWeapon))
        {
            _trayIcon.ShowBalloonTip(2000, "AMacQ", "请先选择枪械。", Forms.ToolTipIcon.Warning);
            return;
        }

        Dispatcher.BeginInvoke(new Action(() =>
        {
            var result = _viewModel.AdjustCurrentWeaponSensitivity(adjustX, direction, step);
            if (!result.IsSuccess)
            {
                _trayIcon.ShowBalloonTip(2000, "AMacQ", result.Error ?? "当前枪械灵敏度调整失败。", Forms.ToolTipIcon.Warning);
                return;
            }

            // 保存由 ViewModel 统一延迟处理：连续调整会不断取消上一次任务，
            // 仅在停止操作后写入一次，避免快速点击造成频繁磁盘写入。
            _viewModel.RefreshSelectedWeaponValues();
            UpdateTrayCurrentWeaponStatus();
        }));
    }

    private void HandleTrayMenuClosing(object? sender, Forms.ToolStripDropDownClosingEventArgs e)
    {
        if (!_keepTrayMenuOpenForSensitivity) return;
        if (e.CloseReason is Forms.ToolStripDropDownCloseReason.ItemClicked or Forms.ToolStripDropDownCloseReason.AppClicked)
        {
            e.Cancel = true;
        }
    }

    private void StartTrayOutsideClickWatcher()
    {
        if (_trayOutsideClickHook != IntPtr.Zero) return;
        _trayOutsideClickHookProc ??= OnTrayOutsideClickEvent;
        _trayOutsideClickHook = SetWinEventHook(EventSystemCaptureStart, EventSystemCaptureStart, IntPtr.Zero, _trayOutsideClickHookProc, 0, 0, WineventOutOfContext);
    }

    private void StopTrayOutsideClickWatcher()
    {
        if (_trayOutsideClickHook == IntPtr.Zero) return;
        UnhookWinEvent(_trayOutsideClickHook);
        _trayOutsideClickHook = IntPtr.Zero;
    }

    private void OnTrayOutsideClickEvent(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (!_trayMenu.Visible) return;

        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!_trayMenu.Visible) return;
            if (_keepTrayMenuOpenForSensitivity) return;
            if (IsPointOverTrayMenu(Forms.Control.MousePosition)) return;
            _trayMenu.Close();
        }));
    }

    private bool IsPointOverTrayMenu(Drawing.Point screenPoint)
    {
        var window = WindowFromPoint(screenPoint);
        if (window == IntPtr.Zero) return false;
        var root = GetAncestor(window, GaRoot);
        if (root == IntPtr.Zero) root = window;
        return IsTrayMenuHandle(root);
    }

    private bool IsTrayMenuHandle(IntPtr handle)
    {
        if (_trayMenu.IsHandleCreated && _trayMenu.Handle == handle) return true;
        return IsDropDownHandle(_trayMenu.Items, handle);
    }

    private static bool IsDropDownHandle(Forms.ToolStripItemCollection items, IntPtr handle)
    {
        foreach (Forms.ToolStripItem item in items)
        {
            if (item is not Forms.ToolStripDropDownItem dropDownItem) continue;
            if (dropDownItem.DropDown.Visible && dropDownItem.DropDown.IsHandleCreated && dropDownItem.DropDown.Handle == handle) return true;
            if (IsDropDownHandle(dropDownItem.DropDownItems, handle)) return true;
        }

        return false;
    }


    private void UpdateTrayCurrentWeaponStatus()
    {
        if (_trayCurrentWeaponStatus is null) return;
        var weapon = string.IsNullOrWhiteSpace(_viewModel.SelectedWeapon) ? "未选择" : GetWeaponDisplayName(_viewModel.SelectedWeapon);
        _trayCurrentWeaponStatus.Text = $"枪械：{weapon}";
        if (_traySensitivityStatus is null) return;
        if (string.IsNullOrWhiteSpace(_viewModel.SelectedWeapon))
        {
            _traySensitivityStatus.Text = "灵敏度：—";
            return;
        }
        var currentWeapon = _viewModel.SelectedWeapon!;
        var sensitivityX = _viewModel.GetSensitivityValue(currentWeapon, "qq1156777787_X");
        var sensitivityY = _viewModel.GetSensitivityValue(currentWeapon, "qq1156777787_Y");
        _traySensitivityStatus.Text = $"灵敏度：X {sensitivityX} / Y {sensitivityY}";
    }

    private static void DisposeMenuItems(Forms.ToolStripItemCollection items)
    {
        // ToolStripItem.Dispose 会将其从父集合移除，因此从末尾向前逐个释放即可遍历完整棵树。
        for (var index = items.Count - 1; index >= 0; index--)
        {
            if (items[index] is Forms.ToolStripDropDownItem { DropDownItems.Count: > 0 } dropDownItem)
            {
                DisposeMenuItems(dropDownItem.DropDownItems);
            }
            items[index].Dispose();
        }
    }

    private static string GetWeaponCategory(string weapon) => weapon switch
    {
        "AK12" or "AKM" or "AR57" or "ASVAL" or "ASH" or "AUG" or "M7" or "CAR15" or "G3" or "K416" or "K437" or "KC17" or "M4A1" or "MCX" or "MDR" or "MK47" or "PTR32" or "QBZ" or "RM277" or "SCAR" or "SG552" or "TJ191" => "突击步枪",
        "MK4" or "MP5" or "MP7" or "QCQ17" or "SR3M" or "TOM" or "UZI" or "Vector" or "YeNiu" => "冲锋枪",
        "M250" or "PKM" or "QJB201" => "轻机枪",
        "M14" or "SVCH" => "射手步枪",
        _ => "其他"
    };

    private static string GetWeaponDisplayName(string? weapon) => WeaponNameMapper.GetDisplayName(weapon);

    private void AddTrayBindingMenu(Forms.ToolStripMenuItem weaponMenu, string weapon, string label, string suffix, string propertyName)
    {
        var bindingMenu = new Forms.ToolStripMenuItem(label)
        {
            DropDownDirection = TraySubMenuDirection
        };
        ConfigureTrayDropDown(bindingMenu.DropDown);
        var currentValue = _viewModel.GetBindingValue(weapon, suffix);
        foreach (var option in KeyOptionsFor(MouseModelList.SelectedValue?.ToString(), currentValue))
        {
            var optionMenu = new Forms.ToolStripMenuItem(option.Text)
            {
                Checked = option.Value == currentValue,
                CheckOnClick = false,
                Tag = option.Value
            };
            optionMenu.Click += (_, _) =>
            {
                ApplyTrayBinding(weapon, propertyName, option.Value ?? "0");
                _trayMenu.Close();
            };
            bindingMenu.DropDownItems.Add(optionMenu);
        }
        weaponMenu.DropDownItems.Add(bindingMenu);
    }

    private void SelectWeaponFromTray(string weapon)
    {
        Dispatcher.BeginInvoke(new Action(() => SelectWeaponFromTrayOnUiThread(weapon)));
    }

    private void SelectWeaponFromTrayOnUiThread(string weapon)
    {
        var item = WeaponList.Items.OfType<WeaponListItem>().FirstOrDefault(candidate => candidate.Name == weapon);
        if (item is null) return;

        if (ReferenceEquals(WeaponList.SelectedItem, item)) SelectWeapon();
        else WeaponList.SelectedItem = item;

        MarkTrayWeaponMenuDirty();
    }

    /// <summary>
    /// 写入指定枪械某个槽位的按键值并立即保存。
    ///
    /// 这里刻意不用 Dispatcher.BeginInvoke 延后：两个调用方（托盘菜单项点击、
    /// 快速绑定流程）都已经在 UI 线程上，延后只会让调用方在返回后读到尚未写入的旧值，
    /// 表现为"绑定成功了但界面还显示旧绑定"。同步执行也让调用方能拿到真实结果。
    /// </summary>
    private void ApplyTrayBinding(string weapon, string propertyName, string value)
    {
        SelectWeaponFromTrayOnUiThread(weapon);
        switch (propertyName)
        {
            case nameof(MainWindowViewModel.PrimaryKey):
                _viewModel.PrimaryKey = value;
                break;
            case nameof(MainWindowViewModel.AltKey):
                _viewModel.AltKey = value;
                break;
            case nameof(MainWindowViewModel.CtrlKey):
                _viewModel.CtrlKey = value;
                break;
        }
        SaveChanges();
        _viewModel.RefreshSelectedWeaponValues();
    }


    private sealed class TrayMenuRenderer : Forms.ToolStripProfessionalRenderer
    {
        private readonly MainWindow _window;

        public TrayMenuRenderer(MainWindow window)
            : base(new TrayMenuColorTable(window))
        {
            _window = window;
            RoundedEdges = true;
        }

        protected override void OnRenderToolStripBackground(Forms.ToolStripRenderEventArgs eventArgs)
        {
            var bounds = eventArgs.AffectedBounds;
            using var brush = new LinearGradientBrush(bounds, _window.GetThemeColor("SurfacePopupStartColor"), _window.GetThemeColor("SurfacePopupEndColor"), 45f);
            eventArgs.Graphics.FillRectangle(brush, bounds);
        }

        protected override void OnRenderToolStripBorder(Forms.ToolStripRenderEventArgs eventArgs)
        {
            // 菜单使用圆角裁剪区域，不绘制额外边框线，避免出现明显的矩形边框。
        }

        protected override void OnRenderMenuItemBackground(Forms.ToolStripItemRenderEventArgs eventArgs)
        {
            if (!eventArgs.Item.Selected || !eventArgs.Item.Enabled) return;

            var bounds = new Drawing.Rectangle(2, 1, eventArgs.Item.Width - 4, eventArgs.Item.Height - 2);
            using var brush = new Drawing.SolidBrush(_window.GetThemeColor("ControlHoverColor"));
            eventArgs.Graphics.FillRectangle(brush, bounds);
        }

        protected override void OnRenderItemText(Forms.ToolStripItemTextRenderEventArgs eventArgs)
        {
            eventArgs.TextColor = eventArgs.Item.Enabled
                ? _window.GetThemeColor("TextPrimaryColor")
                : _window.GetThemeColor("TextSecondaryColor");
            base.OnRenderItemText(eventArgs);
        }

        protected override void OnRenderSeparator(Forms.ToolStripSeparatorRenderEventArgs eventArgs)
        {
            var y = eventArgs.Item.Height / 2;
            using var pen = new Drawing.Pen(_window.GetThemeColor("BorderDividerColor"));
            eventArgs.Graphics.DrawLine(pen, 8, y, eventArgs.Item.Width - 8, y);
        }

        protected override void OnRenderArrow(Forms.ToolStripArrowRenderEventArgs eventArgs)
        {
            eventArgs.ArrowColor = _window.GetThemeColor("TextSecondaryColor");
            base.OnRenderArrow(eventArgs);
        }

        protected override void OnRenderItemCheck(Forms.ToolStripItemImageRenderEventArgs eventArgs)
        {
            var bounds = eventArgs.ImageRectangle;
            using var brush = new Drawing.SolidBrush(_window.GetThemeColor("AccentCyanColor"));
            eventArgs.Graphics.FillRectangle(brush, bounds);
            using var pen = new Drawing.Pen(_window.GetThemeColor("AccentForegroundColor"), 2f);
            eventArgs.Graphics.DrawLines(pen, new[]
            {
                new Drawing.Point(bounds.Left + 3, bounds.Top + bounds.Height / 2),
                new Drawing.Point(bounds.Left + bounds.Width / 2 - 1, bounds.Bottom - 4),
                new Drawing.Point(bounds.Right - 3, bounds.Top + 4)
            });
        }
    }

    private sealed class TrayMenuColorTable : Forms.ProfessionalColorTable
    {
        private readonly MainWindow _window;

        public TrayMenuColorTable(MainWindow window)
        {
            _window = window;
            UseSystemColors = false;
        }

        public override Drawing.Color MenuBorder => _window.GetThemeColor("BorderPanelColor");
        public override Drawing.Color MenuItemBorder => _window.GetThemeColor("BorderFocusColor");
        public override Drawing.Color MenuItemSelected => _window.GetThemeColor("ControlHoverColor");
        public override Drawing.Color MenuItemSelectedGradientBegin => _window.GetThemeColor("ControlHoverColor");
        public override Drawing.Color MenuItemSelectedGradientEnd => _window.GetThemeColor("ControlHoverColor");
        public override Drawing.Color ToolStripDropDownBackground => _window.GetThemeColor("SurfacePopupEndColor");
        public override Drawing.Color ImageMarginGradientBegin => _window.GetThemeColor("SurfacePopupStartColor");
        public override Drawing.Color ImageMarginGradientMiddle => _window.GetThemeColor("SurfacePopupStartColor");
        public override Drawing.Color ImageMarginGradientEnd => _window.GetThemeColor("SurfacePopupEndColor");
        public override Drawing.Color SeparatorDark => _window.GetThemeColor("BorderDividerColor");
        public override Drawing.Color SeparatorLight => _window.GetThemeColor("BorderDividerColor");
    }

    private Drawing.Color GetThemeColor(string key)
    {
        if (Resources[key] is System.Windows.Media.Color color)
            return Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);
        return Drawing.Color.FromArgb(255, 15, 32, 56);
    }

}
