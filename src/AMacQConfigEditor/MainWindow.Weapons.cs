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
    private void LoadDefaultFilesIfAvailable()
    {
        _keyBindingsPath = ObscuredPackageDeploymentService.GetInstalledConfigurationPath("sorinkg.lua");
        _sensitivityPath = ObscuredPackageDeploymentService.GetInstalledConfigurationPath("sorinxs.lua");
        if (_keyBindingsPath is not null && _sensitivityPath is not null) LoadFiles();
        else MarkTrayWeaponMenuDirty();
    }

    private void LoadFiles()
    {
        var result = _viewModel.Load(_keyBindingsPath!, _sensitivityPath!);
        if (!result.IsSuccess)
        {
            MessageBox.Show(result.Error, "加载失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // 配置里的触发方式/增幅激活键可能不在预设选项内，加载后重建选项列表以保留原值。
        RefreshGlobalOptionLists();
        RefreshWeaponList();
        MarkTrayWeaponMenuDirty();
    }

    private void RefreshWeaponList(string? selectedWeapon = null)
    {
        selectedWeapon ??= (WeaponList.SelectedItem as WeaponListItem)?.Name;
        _allWeaponItems = _viewModel.Weapons.Select(name => new WeaponListItem(name, _viewModel.GetBindingSummary(name))).ToArray();
        ApplyWeaponFilter(selectedWeapon);
    }

    /// <summary>
    /// 按搜索框内容过滤枪械列表：中文显示名与内部代码都可匹配，支持多关键词（空格分隔）
    /// 与子串匹配，因此“快切”时输入任意片段即可定位。
    /// </summary>
    private void ApplyWeaponFilter(string? preferredSelection = null)
    {
        var query = WeaponSearchBox.Text?.Trim() ?? string.Empty;
        var items = WeaponFilter.Apply(_allWeaponItems, query, item => item.Name, item => item.DisplayName, item => item.BindingSummary);
        var totalCount = _allWeaponItems.Length;

        UpdateWeaponSearchPlaceholder();
        WeaponSearchCount.Text = query.Length == 0 ? string.Empty : $"{items.Count}/{totalCount}";
        WeaponList.ItemsSource = items;

        var target = items.FirstOrDefault(item => item.Name == preferredSelection)
            ?? items.FirstOrDefault(item => item.Name == _viewModel.SelectedWeapon)
            ?? items.FirstOrDefault();
        if (!ReferenceEquals(WeaponList.SelectedItem, target)) WeaponList.SelectedItem = target;
        if (target is not null) WeaponList.ScrollIntoView(target);

        SaveBtn.IsEnabled = items.Count > 0;
    }

    private void HandleWeaponSearchTextChanged(object sender, TextChangedEventArgs args)
    {
        if (!IsLoaded) return;
        ApplyWeaponFilter(WeaponList.SelectedItem as WeaponListItem is { } selected ? selected.Name : null);
    }

    private void HandleWeaponSearchKeyDown(object sender, System.Windows.Input.KeyEventArgs args)
    {
        switch (args.Key)
        {
            case Key.Escape:
                args.Handled = true;
                ClearWeaponSearch();
                break;
            case Key.Enter:
                args.Handled = true;
                WeaponList.Focus();
                if (WeaponList.SelectedItem is WeaponListItem enterTarget) WeaponList.ScrollIntoView(enterTarget);
                break;
            case Key.Down:
            case Key.Up:
                // 方向键直接接管列表选择，避免用户搜到目标后还要再点一次列表。
                args.Handled = true;
                MoveWeaponSelection(args.Key == Key.Down ? 1 : -1);
                break;
        }
    }

    private void MoveWeaponSelection(int offset)
    {
        if (WeaponList.Items.Count == 0) return;

        var index = WeaponList.SelectedIndex;
        var lastIndex = WeaponList.Items.Count - 1;
        var target = index < 0 ? (offset > 0 ? 0 : lastIndex) : index + offset;
        var next = Math.Max(0, Math.Min(target, lastIndex));
        if (next == index) return;

        WeaponList.SelectedIndex = next;
        if (WeaponList.SelectedItem is WeaponListItem item) WeaponList.ScrollIntoView(item);
    }

    private void ClearWeaponSearch()
    {
        if (WeaponSearchBox.Text.Length == 0)
        {
            WeaponList.Focus();
            return;
        }

        WeaponSearchBox.Clear();
        WeaponSearchBox.Focus();
    }

    private void FocusWeaponSearch(object sender, RoutedEventArgs args) => UpdateWeaponSearchPlaceholder();

    private void BlurWeaponSearch(object sender, RoutedEventArgs args) => UpdateWeaponSearchPlaceholder();

    /// <summary>
    /// 占位提示只在"输入框为空且没有焦点"时显示。
    /// 如果获得焦点后仍然显示，光标会叠在占位文字上，看起来就像光标位置不对。
    /// </summary>
    private void UpdateWeaponSearchPlaceholder() =>
        WeaponSearchPlaceholder.Visibility = WeaponSearchBox.Text.Length == 0 && !WeaponSearchBox.IsKeyboardFocusWithin
            ? Visibility.Visible
            : Visibility.Collapsed;

    private void SelectWeapon()
    {
        if (WeaponList.SelectedItem is not WeaponListItem weapon) return;
        _viewModel.SelectedWeapon = weapon.Name;

        // 必须把枪械名传进去：切换枪械时下拉框可能因为选项列表里没有该键值（6/7/8/9
        // 这类不在按鼠标型号生成的列表里）而被清空，并把这个空值写回 ViewModel。
        // 传入枪械名后改用配置文档里的真实键值重建选项，顺带把被清空的值纠正回来。
        RefreshKeyOptions(weapon.Name);
        SelectedLabel.Text = "当前枪械：";
        SelectedWeaponLabel.Text = weapon.DisplayName;
        UpdateTrayCurrentWeaponStatus();
    }

    private void OnWeaponSelectionChanged()
    {
        // 主窗口选中项变化本身不触发托盘菜单重建：
        // 托盘菜单在用户打开时按需刷新（左键打开托盘），避免一次保存流程触发多次重建。
        if (WeaponList.SelectedItem is WeaponListItem)
        {
            SelectWeapon();
            MarkTrayWeaponMenuDirty();
        }
    }

    private void BuildFieldCards()
    {
        FieldCards.Children.Clear();
        FieldCards.Children.Add(BuildFieldSection("按键", [
            ("无修饰键", nameof(MainWindowViewModel.PrimaryKey)),
            ("按住 Alt", nameof(MainWindowViewModel.AltKey)),
            ("按住 Ctrl", nameof(MainWindowViewModel.CtrlKey))]));
        FieldCards.Children.Add(BuildFieldSection("灵敏度", [
            ("灵敏度 X", nameof(MainWindowViewModel.SensitivityX)),
            ("灵敏度 Y", nameof(MainWindowViewModel.SensitivityY)),
            ("灵敏度 增幅 X", nameof(MainWindowViewModel.SensitivityAddX)),
            ("灵敏度 增幅 Y", nameof(MainWindowViewModel.SensitivityAddY))]));
    }

    private StackPanel BuildFieldSection(string title, (string Label, string Property)[] fields)
    {
        var section = new StackPanel { Margin = new Thickness(title == "按键" ? 0 : 8, 0, title == "按键" ? 8 : 0, 0) };
        section.Children.Add(new TextBlock { Text = title, FontSize = 12, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8), Foreground = (System.Windows.Media.Brush)FindResource("SecondaryTextBrush") });
        var list = new StackPanel();
        var outer = new Border { Background = (System.Windows.Media.Brush)FindResource("PanelSurfaceBrush"), BorderBrush = (System.Windows.Media.Brush)FindResource("ControlBorderBrush"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Child = list };
        section.Children.Add(outer);
        foreach (var field in fields)
        {
            var row = new Grid { Height = 44 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            var label = new TextBlock { Text = field.Label, FontSize = 13, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 8, 0), Foreground = (System.Windows.Media.Brush)FindResource("BodyTextBrush") };
            row.Children.Add(label);
            var isKeyField = field.Property is nameof(MainWindowViewModel.PrimaryKey) or nameof(MainWindowViewModel.AltKey) or nameof(MainWindowViewModel.CtrlKey);
            Control input;
            if (isKeyField)
            {
                var combo = new ComboBox { Height = 30, Width = 140, FontSize = 13, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 10, 0), Style = (Style)FindResource("DarkComboBox"), DisplayMemberPath = nameof(KeyOption.Text), SelectedValuePath = nameof(KeyOption.Value), ItemsSource = KeyOptions };
                combo.SetBinding(ComboBox.SelectedValueProperty, new Binding(field.Property) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
                input = combo;
            }
            else
            {
                var text = new TextBox { Height = 30, Width = 140, Padding = new Thickness(8, 2, 8, 2), FontSize = 13, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 10, 0), Style = (Style)FindResource("DarkTextBox") };
                text.SetBinding(TextBox.TextProperty, new Binding(field.Property) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
                text.PreviewTextInput += ValidateSensitivityTextInput;
                text.PreviewKeyDown += AdjustSensitivityWithArrowKeys;
                DataObject.AddPastingHandler(text, ValidateSensitivityPaste);
                input = text;
            }
            Grid.SetColumn(input, 1);
            row.Children.Add(input);
            _fieldInputs[field.Property] = input;
            list.Children.Add(row);
        }
        return section;
    }

    private void PopulateGlobalOptions()
    {
        PressList.DisplayMemberPath = nameof(SelectionOption.Text); PressList.SelectedValuePath = nameof(SelectionOption.Value);
        PressList.SetBinding(ComboBox.SelectedValueProperty, new Binding(nameof(MainWindowViewModel.Press)) { Mode = BindingMode.TwoWay });
        ModeSwitchList.DisplayMemberPath = nameof(SelectionOption.Text); ModeSwitchList.SelectedValuePath = nameof(SelectionOption.Value);
        ModeSwitchList.SetBinding(ComboBox.SelectedValueProperty, new Binding(nameof(MainWindowViewModel.ModeSwitch)) { Mode = BindingMode.TwoWay });
        RefreshGlobalOptionLists();
        MouseModelList.DisplayMemberPath = nameof(SelectionOption.Text); MouseModelList.SelectedValuePath = nameof(SelectionOption.Value);
        MouseModelList.ItemsSource = new[] { new SelectionOption("通用双侧键鼠标", "generic"), new SelectionOption("G102", "g102"), new SelectionOption("G304 / G305", "g304"), new SelectionOption("G Pro Wireless（GPW）", "gpw"), new SelectionOption("G Pro X Superlight（GPX）", "gpw"), new SelectionOption("G402", "g402"), new SelectionOption("G502 Hero", "g502hero"), new SelectionOption("G502 X", "g502x") };
        MouseModelList.SelectionChanged += (_, _) =>
        {
            RefreshKeyOptions();
            MarkTrayWeaponMenuDirty();
        };
        MouseModelList.SelectedIndex = 0;
    }

    /// <summary>
    /// 重建"触发方式 / 增幅激活键"的选项，并把配置里已有的值作为兜底项加进去。
    ///
    /// 下拉框表示不了某个值时会把绑定源清成 null，随后保存就会失败。
    /// 配置里出现预设之外的值（例如手改过 Lua）时，靠这个兜底项保证原值可显示、可保留。
    /// </summary>
    private void RefreshGlobalOptionLists()
    {
        PressList.ItemsSource = WithCurrentValue(
            [new SelectionOption("鼠标左键", "1"), new SelectionOption("按住右键 + 鼠标左键", "3")],
            _viewModel.Press,
            "当前配置");

        ModeSwitchList.ItemsSource = WithCurrentValue(
            [
                new SelectionOption("Scroll Lock", "scrolllock"),
                new SelectionOption("Caps Lock", "capslock"),
                new SelectionOption("Num Lock", "numlock")
            ],
            _viewModel.ModeSwitch,
            "当前配置");
    }

    private static IReadOnlyList<SelectionOption> WithCurrentValue(IReadOnlyList<SelectionOption> options, string? currentValue, string labelPrefix)
    {
        if (string.IsNullOrWhiteSpace(currentValue)) return options;
        if (options.Any(option => option.Value == currentValue)) return options;

        var extended = options.ToList();
        extended.Add(new SelectionOption($"{labelPrefix}({currentValue})", currentValue!));
        return extended;
    }

    private void SaveChanges()
    {
        try
        {
            _viewModel.Save();
            SaveBtn.Content = "应用成功";
            RefreshWeaponList(_viewModel.SelectedWeapon);
            MarkTrayWeaponMenuDirty();
            _saveResetTimer.Stop();
            _saveResetTimer.Start();
        }
        catch (Exception exception)
        {
            SaveBtn.Content = "应用";
            MessageBox.Show(exception.Message, "保存失败", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static void ValidateSensitivityTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is not TextBox textBox) return;
        var proposed = textBox.Text.Remove(textBox.SelectionStart, textBox.SelectionLength).Insert(textBox.SelectionStart, e.Text);
        e.Handled = !IsPotentialSensitivityValue(proposed);
    }

    private static void AdjustSensitivityWithArrowKeys(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox || (e.Key is not Key.Up and not Key.Down)) return;
        textBox.Text = MainWindowViewModel.AdjustSensitivityValue(textBox.Text, e.Key == Key.Up ? 1 : -1);
        textBox.CaretIndex = textBox.Text.Length;
        e.Handled = true;
    }


    private sealed record WeaponListItem(string Name, string BindingSummary)
    {
        public string DisplayName => GetWeaponDisplayName(Name);
        public bool HasBindingSummary => !string.IsNullOrWhiteSpace(BindingSummary);
    }

    /// <summary>
    /// 重建三个按键下拉框的选项。
    ///
    /// 切换枪械时传入 <paramref name="weapon"/>：此时下拉框可能因为选项列表里没有该键值
    /// （6/7/8/9 这类不在按鼠标型号生成的列表里）而被清空并把空值写回 ViewModel，
    /// 所以这里改用配置文档里的真实键值来重建选项，再写回下拉框把值纠正回来。
    /// 不传枪械名（例如用户切换鼠标型号）时沿用下拉框当前值。
    /// </summary>
    private void RefreshKeyOptions(string? weapon = null)
    {
        var mouseModel = MouseModelList.SelectedValue?.ToString();
        foreach (var entry in _fieldInputs)
        {
            if (entry.Value is not ComboBox combo) continue;
            if (SlotFor(entry.Key) is not { } slot) continue;

            var currentValue = weapon is null
                ? combo.SelectedValue?.ToString()
                : _viewModel.GetSlotBindingValue(weapon, slot);

            combo.ItemsSource = KeyOptionsFor(mouseModel, currentValue);
            combo.SelectedValue = currentValue;
        }
    }

    /// <summary>按键字段名到槽位的映射；非按键字段返回 null。</summary>
    private static WeaponBindingSlot? SlotFor(string property) => property switch
    {
        nameof(MainWindowViewModel.PrimaryKey) => WeaponBindingSlot.Primary,
        nameof(MainWindowViewModel.AltKey) => WeaponBindingSlot.Alt,
        nameof(MainWindowViewModel.CtrlKey) => WeaponBindingSlot.Ctrl,
        _ => null
    };

    private static IReadOnlyList<KeyOption> KeyOptionsFor(string? mouseModel, string? currentValue)
    {
        var options = new List<KeyOption> { new("无按键(0)", "0"), new("左侧后退键(4)", "4"), new("左侧前进键(5)", "5") };
        if (mouseModel == "gpw")
        {
            options.Add(new("右侧后退键(7)", "7"));
            options.Add(new("右侧前进键(8)", "8"));
        }
        if (!string.IsNullOrWhiteSpace(currentValue) && options.All(option => option.Value != currentValue))
        {
            options.Add(new($"当前配置({currentValue})", currentValue));
        }
        return options;
    }

    private static IReadOnlyList<KeyOption> KeyOptions { get; } = KeyOptionsFor("generic", null);
    private sealed record KeyOption(string Text, string? Value);
    private sealed record SelectionOption(string Text, string Value);

    private static void ValidateSensitivityPaste(object sender, DataObjectPastingEventArgs e)
    {
        if (sender is not TextBox textBox || !e.DataObject.GetDataPresent(DataFormats.UnicodeText)) { e.CancelCommand(); return; }
        var pasted = e.DataObject.GetData(DataFormats.UnicodeText) as string ?? string.Empty;
        var proposed = textBox.Text.Remove(textBox.SelectionStart, textBox.SelectionLength).Insert(textBox.SelectionStart, pasted);
        if (!MainWindowViewModel.IsValidSensitivityValue(proposed)) e.CancelCommand();
    }

    private static bool IsPotentialSensitivityValue(string value) =>
        value.Length == 0 || Regex.IsMatch(value, "^\\d*(?:\\.\\d{0,2})?$");
}
