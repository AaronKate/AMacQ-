using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using AMacQConfigEditor.Services;
using AMacQConfigEditor.ViewModels;

namespace AMacQConfigEditor;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel = new();
    private readonly Dictionary<string, Control> _fieldInputs = [];
    private string? _keyBindingsPath;
    private string? _sensitivityPath;
    private readonly DispatcherTimer _saveResetTimer = new() { Interval = TimeSpan.FromSeconds(1.5) };

    public MainWindow()
    {
        InitializeComponent();
        TechnologyThemeService.ApplyRandomTheme(this);
        DataContext = _viewModel;
        SetWindowIcon();

        DecompressBtn.Click += (_, _) => DeployEmbeddedPackage();
        SaveBtn.Click += (_, _) => SaveChanges();
        MinimizeBtn.Click += (_, _) => WindowState = WindowState.Minimized;
        CloseBtn.Click += (_, _) => Close();
        WeaponList.SelectionChanged += (_, _) => SelectWeapon();
        WeaponList.ItemContainerStyle = (Style)FindResource("WeaponListItem");

        BuildFieldCards();
        PopulateGlobalOptions();
        LoadDefaultFilesIfAvailable();
        _saveResetTimer.Tick += (_, _) => { SaveBtn.Content = "应用"; _saveResetTimer.Stop(); };
    }

    private async void DeployEmbeddedPackage()
    {
        try
        {
            DecompressBtn.IsEnabled = false;
            DeploymentStatusText.Text = "正在解压资源包…";
            var result = await Task.Run(() => EmbeddedPackageDeploymentService.Deploy(@"C:\"));
            LoadDefaultFilesIfAvailable();
            DeploymentStatusText.Text = result.ExtractedTargets.Count > 0
                ? "部署完成，已就绪"
                : "已检查，资源已存在";
            MessageBox.Show(result.ToDisplayMessage(), "部署完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            DeploymentStatusText.Text = "部署失败，请检查权限";
            MessageBox.Show(exception.Message, "解压失败", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            DecompressBtn.IsEnabled = true;
        }
    }

    private void LoadDefaultFilesIfAvailable()
    {
        _keyBindingsPath = @"C:\AMacQ1156777787\sorinkg.lua";
        _sensitivityPath = @"C:\AMacQ1156777787\sorinxs.lua";
        if (File.Exists(_keyBindingsPath) && File.Exists(_sensitivityPath)) LoadFiles();
    }

    private void LoadFiles()
    {
        var result = _viewModel.Load(_keyBindingsPath!, _sensitivityPath!);
        if (!result.IsSuccess)
        {
            MessageBox.Show(result.Error, "加载失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        RefreshWeaponList();
    }

    private void RefreshWeaponList(string? selectedWeapon = null)
    {
        selectedWeapon ??= (WeaponList.SelectedItem as WeaponListItem)?.Name;
        var weapons = _viewModel.Weapons.Select(name => new WeaponListItem(name, _viewModel.GetBindingSummary(name))).ToArray();
        WeaponList.ItemsSource = weapons;
        WeaponList.SelectedItem = weapons.FirstOrDefault(weapon => weapon.Name == selectedWeapon) ?? weapons.FirstOrDefault();
        SaveBtn.IsEnabled = weapons.Length > 0;
    }

    private void SelectWeapon()
    {
        if (WeaponList.SelectedItem is not WeaponListItem weapon) return;
        _viewModel.SelectedWeapon = weapon.Name;
        RefreshKeyOptions();
        SelectedLabel.Text = "当前枪械：";
        SelectedWeaponLabel.Text = weapon.Name;
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
        PressList.ItemsSource = new[] { new SelectionOption("鼠标左键", "1"), new SelectionOption("按住右键 + 鼠标左键", "3") };
        PressList.SetBinding(ComboBox.SelectedValueProperty, new Binding(nameof(MainWindowViewModel.Press)) { Mode = BindingMode.TwoWay });
        ModeSwitchList.DisplayMemberPath = nameof(SelectionOption.Text); ModeSwitchList.SelectedValuePath = nameof(SelectionOption.Value);
        ModeSwitchList.ItemsSource = new[] { new SelectionOption("Scroll Lock", "scrolllock"), new SelectionOption("Caps Lock", "capslock"), new SelectionOption("Num Lock", "numlock") };
        ModeSwitchList.SetBinding(ComboBox.SelectedValueProperty, new Binding(nameof(MainWindowViewModel.ModeSwitch)) { Mode = BindingMode.TwoWay });
        MouseModelList.DisplayMemberPath = nameof(SelectionOption.Text); MouseModelList.SelectedValuePath = nameof(SelectionOption.Value);
        MouseModelList.ItemsSource = new[] { new SelectionOption("通用双侧键鼠标", "generic"), new SelectionOption("G102", "g102"), new SelectionOption("G304 / G305", "g304"), new SelectionOption("G Pro Wireless（GPW）", "gpw"), new SelectionOption("G Pro X Superlight（GPX）", "gpw"), new SelectionOption("G402", "g402"), new SelectionOption("G502 Hero", "g502hero"), new SelectionOption("G502 X", "g502x") };
        MouseModelList.SelectionChanged += (_, _) => RefreshKeyOptions();
        MouseModelList.SelectedIndex = 0;
    }

    private void SaveChanges()
    {
        try
        {
            _viewModel.Save();
            SaveBtn.Content = "应用成功";
            RefreshWeaponList(_viewModel.SelectedWeapon);
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

    private static void ValidateSensitivityPaste(object sender, DataObjectPastingEventArgs e)
    {
        if (sender is not TextBox textBox || !e.DataObject.GetDataPresent(DataFormats.UnicodeText)) { e.CancelCommand(); return; }
        var pasted = e.DataObject.GetData(DataFormats.UnicodeText) as string ?? string.Empty;
        var proposed = textBox.Text.Remove(textBox.SelectionStart, textBox.SelectionLength).Insert(textBox.SelectionStart, pasted);
        if (!MainWindowViewModel.IsValidSensitivityValue(proposed)) e.CancelCommand();
    }

    private static bool IsPotentialSensitivityValue(string value) =>
        value.Length == 0 || Regex.IsMatch(value, "^\\d*(?:\\.\\d{0,2})?$");

    private void SetWindowIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "AMacQ.ico");
        if (!File.Exists(iconPath)) return;

        Icon = System.Windows.Media.Imaging.BitmapFrame.Create(new Uri(iconPath));
        TitleBarIcon.Source = Icon;
    }

    private sealed record WeaponListItem(string Name, string BindingSummary)
    {
        public bool HasBindingSummary => !string.IsNullOrWhiteSpace(BindingSummary);
    }

    private void RefreshKeyOptions()
    {
        foreach (var combo in _fieldInputs.Values.OfType<ComboBox>())
        {
            var currentValue = combo.SelectedValue?.ToString();
            combo.ItemsSource = KeyOptionsFor(MouseModelList.SelectedValue?.ToString(), currentValue);
            combo.SelectedValue = currentValue;
        }
    }

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
    private sealed record KeyOption(string Text, string Value);
    private sealed record SelectionOption(string Text, string Value);
}
