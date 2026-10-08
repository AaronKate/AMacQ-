using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Forms = System.Windows.Forms;

namespace AMacQConfigEditor;

/// <summary>
/// 快速切换窗口与宿主窗口之间的契约。抽成接口后，窗口只依赖这份契约，
/// 既可以由主窗口驱动，也可以在验证夹具里用假宿主驱动。
/// </summary>
internal interface IWeaponQuickSwitchHost
{
    IReadOnlyList<string> WeaponNames { get; }

    string DisplayNameFor(string weapon);

    string BindingSummaryFor(string weapon);

    string? CurrentWeaponName { get; }

    /// <summary>
    /// 切换当前枪械并立即保存，然后进入"等待按侧键"的绑定监听：窗口已关闭，
    /// 用户直接在后台按一下侧键（或数字键）即完成该槽位的绑定。
    /// 只想换枪不想绑键时不按任何键即可，监听会自动超时结束。
    /// </summary>
    void ConfirmWeapon(string weapon, WeaponBindingSlot slot);

    /// <summary>读取指定枪械某个槽位当前的按键值（"0" 表示未绑定）。</summary>
    string BindingValueFor(string weapon, WeaponBindingSlot slot);

    /// <summary>写入按键值并立即保存；返回被顶掉绑定的其它枪械名（没有冲突时为空）。</summary>
    string? ApplyBinding(string weapon, WeaponBindingSlot slot, string value);
}

/// <summary>
/// 供游戏内使用的纯键盘枪械切换窗口。由全局快捷键唤起后：
/// 直接输入关键字筛选（中文名、内部代码、已绑按键都能匹配），↑/↓ 移动，
/// Enter 切换当前枪械，Esc 取消。全程不需要鼠标，也不需要展开多级菜单。
/// </summary>
public partial class QuickSwitchWindow : Window
{
    private readonly IWeaponQuickSwitchHost _host;
    private readonly List<WeaponRow> _rows = [];

    internal QuickSwitchWindow(IWeaponQuickSwitchHost host)
    {
        _host = host;
        InitializeComponent();
        Services.TechnologyThemeService.ApplyCurrentTheme(this);

        _rows.AddRange(host.WeaponNames.Select(name => new WeaponRow(name, host.DisplayNameFor(name), host.BindingSummaryFor(name))));

        SearchBox.TextChanged += (_, _) => ApplyFilter();
        SearchBox.PreviewKeyDown += HandleSearchKeyDown;
        SearchBox.GotKeyboardFocus += (_, _) => SearchPlaceholder.Visibility = Visibility.Collapsed;
        SearchBox.LostKeyboardFocus += (_, _) => UpdatePlaceholder();
        WeaponList.MouseDoubleClick += (_, _) => ConfirmSelection(Keyboard.Modifiers);
        Loaded += (_, _) =>
        {
            PositionNearCursor();
            ApplyFilter();
            FocusSearch();
        };
    }

    public void FocusSearch()
    {
        SearchBox.Focus();
        Keyboard.Focus(SearchBox);
        SearchBox.SelectAll();
    }

    private void ApplyFilter()
    {
        var query = SearchBox.Text?.Trim() ?? string.Empty;
        var matches = ViewModels.WeaponFilter.Apply(_rows, query, row => row.Name, row => row.DisplayName, row => row.Summary).ToArray();

        UpdatePlaceholder();
        SearchCount.Text = query.Length == 0 ? string.Empty : $"{matches.Length}/{_rows.Count}";
        EmptyHint.Visibility = matches.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

        WeaponList.ItemsSource = matches;

        // 每次筛选都把选中项落到第一条命中，因此"打字 + Enter"两步即可完成切换。
        if (matches.Length > 0) SelectIndex(0);
        else UpdateCurrentWeaponText();
    }

    private void UpdatePlaceholder() =>
        SearchPlaceholder.Visibility = SearchBox.Text.Length == 0 && !SearchBox.IsKeyboardFocusWithin
            ? Visibility.Visible
            : Visibility.Collapsed;

    /// <summary>
    /// 底栏分两段：左边是"当前生效的枪械"（取自宿主，不随列表移动而变），
    /// 右边是"按 Enter 会切到哪把枪"（即列表高亮项）。两者含义不同，不能混用。
    /// </summary>
    private void UpdateCurrentWeaponText()
    {
        var current = _host.CurrentWeaponName;
        CurrentWeaponText.Text = string.IsNullOrWhiteSpace(current)
            ? "尚未选择枪械"
            : $"当前：{Describe(current!)}";

        var pending = (WeaponList.SelectedItem as WeaponRow)?.Name;
        PendingWeaponText.Text = string.IsNullOrWhiteSpace(pending) || string.Equals(pending, current, StringComparison.Ordinal)
            ? string.Empty
            : $"Enter → {Describe(pending!)}";
    }

    private string Describe(string weapon)
    {
        var summary = _host.BindingSummaryFor(weapon);
        return _host.DisplayNameFor(weapon) + (string.IsNullOrWhiteSpace(summary) ? " · 未绑定按键" : $" · {summary}");
    }

    private void HandleSearchKeyDown(object sender, System.Windows.Input.KeyEventArgs args)
    {
        var modifiers = args.KeyboardDevice.Modifiers;
        args.Handled = HandleSearchKey(args.Key, modifiers);
    }

    /// <summary>返回是否已处理该按键。与真实键盘事件共用同一份判定逻辑。</summary>
    private bool HandleSearchKey(Key key, ModifierKeys modifiers)
    {
        switch (key)
        {
            case Key.Escape:
                Close();
                return true;
            case Key.Enter:
                ConfirmSelection(modifiers);
                return true;
            case Key.Down:
                MoveSelection(1);
                return true;
            case Key.Up:
                MoveSelection(-1);
                return true;
            case Key.PageDown:
                MoveSelection(8);
                return true;
            case Key.PageUp:
                MoveSelection(-8);
                return true;
            case Key.Home:
                SelectIndex(0);
                return true;
            case Key.End:
                SelectIndex(WeaponList.Items.Count - 1);
                return true;
            default:
                return false;
        }
    }

    private void MoveSelection(int offset)
    {
        if (WeaponList.Items.Count == 0) return;

        var index = WeaponList.SelectedIndex < 0 ? 0 : WeaponList.SelectedIndex + offset;
        SelectIndex(index);
    }

    private void SelectIndex(int index)
    {
        if (WeaponList.Items.Count == 0) return;

        var clamped = Math.Max(0, Math.Min(index, WeaponList.Items.Count - 1));
        WeaponList.SelectedIndex = clamped;
        WeaponList.UpdateLayout();
        if (WeaponList.SelectedItem is WeaponRow row) WeaponList.ScrollIntoView(row);
        UpdateCurrentWeaponText();
    }

    private void ConfirmSelection(ModifierKeys modifiers)
    {
        if (WeaponList.SelectedItem is not WeaponRow row) return;

        // Alt+Enter / Ctrl+Enter 直接指定要绑定的槽位；单独 Enter 绑主键。
        // 修饰键取自键盘事件本身，而不是全局 Keyboard.Modifiers，行为更确定。
        var slot = (modifiers & ModifierKeys.Control) != 0
            ? WeaponBindingSlot.Ctrl
            : (modifiers & ModifierKeys.Alt) != 0
                ? WeaponBindingSlot.Alt
                : WeaponBindingSlot.Primary;

        _host.ConfirmWeapon(row.Name, slot);
        Close();
    }

    private void PositionNearCursor()
    {
        var screen = Forms.Screen.FromPoint(Forms.Cursor.Position);
        var source = PresentationSource.FromVisual(this);
        var transform = source?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        var topLeft = transform.Transform(new Point(screen.WorkingArea.Left, screen.WorkingArea.Top));
        var bottomRight = transform.Transform(new Point(screen.WorkingArea.Right, screen.WorkingArea.Bottom));
        var cursor = transform.Transform(new Point(Forms.Cursor.Position.X, Forms.Cursor.Position.Y));

        var left = cursor.X - ActualWidth / 2;
        var top = cursor.Y - ActualHeight / 2;
        Left = Math.Max(topLeft.X + 16, Math.Min(left, bottomRight.X - ActualWidth - 16));
        Top = Math.Max(topLeft.Y + 16, Math.Min(top, bottomRight.Y - ActualHeight - 16));
    }

    private sealed record WeaponRow(string Name, string DisplayName, string Summary)
    {
        public bool HasSummary => !string.IsNullOrWhiteSpace(Summary);
    }
}
