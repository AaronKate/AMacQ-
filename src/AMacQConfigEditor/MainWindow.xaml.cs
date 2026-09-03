using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Drawing.Drawing2D;
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

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel = new();
    private readonly Dictionary<string, Control> _fieldInputs = [];
    private string? _keyBindingsPath;
    private string? _sensitivityPath;
    private readonly DispatcherTimer _saveResetTimer = new() { Interval = TimeSpan.FromSeconds(1.5) };
    private readonly DispatcherTimer _weaponSearchTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly DispatcherTimer _hotKeyNotificationTimer = new() { Interval = TimeSpan.FromMilliseconds(1500) };
    private readonly Forms.NotifyIcon _trayIcon = new();
    private const int WmHotKey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModNoRepeat = 0x4000;
    private const int HotKeyXDecrease = 1;
    private const int HotKeyXIncrease = 2;
    private const int HotKeyYDecrease = 3;
    private const int HotKeyYIncrease = 4;
    private const int HotKeyStep = 5;
    private const int TrayMenuCornerRadius = 10;
    private readonly Forms.ContextMenuStrip _trayMenu = new();
    private readonly HashSet<Forms.ToolStripDropDown> _roundedTrayMenus = [];
    private Forms.ToolStripMenuItem? _trayCurrentWeaponStatus;
    private Forms.ToolStripMenuItem? _trayWeaponMenu;
    private HwndSource? _windowSource;
    private IntPtr _windowHandle;
    private readonly HashSet<int> _registeredHotKeys = [];
    private string _weaponSearchPrefix = string.Empty;
    private string? _pendingHotKeyNotification;
    private Forms.ToolTipIcon _pendingHotKeyNotificationIcon = Forms.ToolTipIcon.Info;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => InitializeGlobalHotKeys();
        TechnologyThemeService.ApplyRandomTheme(this);
        DataContext = _viewModel;
        SetWindowIcon();
        ConfigureTrayIcon();
        UpdateLicenseStatus();
        ObscuredPackageDeploymentService.RestoreRuntimeConfigurationFiles();

        DecompressBtn.Click += (_, _) =>
        {
            if (!LogitechGHubLauncher.IsInstalled())
            {
                ConfirmOpenDownloadPage();
                return;
            }
            DeployEmbeddedPackage();
        };
        DeploymentDialogCloseButton.Click += (_, _) => CloseDeploymentDialog();
        DownloadConfirmCancelButton.Click += (_, _) => DownloadConfirmOverlay.Visibility = Visibility.Collapsed;
        DownloadConfirmOkButton.Click += (_, _) =>
        {
            DownloadConfirmOverlay.Visibility = Visibility.Collapsed;
            LogitechGHubLauncher.OpenDownloadPage();
        };
        HelpBtn.Click += (_, _) => new HelpWindow(this).ShowDialog();
        SaveBtn.Click += (_, _) => SaveChanges();
        MinimizeBtn.Click += (_, _) => HideToTray();
        CloseBtn.Click += (_, _) => Close();
        Closing += (_, _) => ObscuredPackageDeploymentService.DisableRuntimeConfigurationFiles();
        StateChanged += (_, _) => { if (WindowState == WindowState.Minimized) HideToTray(); };
        WeaponList.SelectionChanged += (_, _) => SelectWeapon();
        WeaponList.PreviewTextInput += (_, args) => FindWeaponByPrefix(args);
        WeaponList.ItemContainerStyle = (Style)FindResource("WeaponListItem");

        BuildFieldCards();
        PopulateGlobalOptions();
        LoadDefaultFilesIfAvailable();
        _saveResetTimer.Tick += (_, _) => { SaveBtn.Content = "应用"; _saveResetTimer.Stop(); };
        _weaponSearchTimer.Tick += (_, _) => { _weaponSearchPrefix = string.Empty; _weaponSearchTimer.Stop(); };
        _hotKeyNotificationTimer.Tick += (_, _) => FlushHotKeyNotification();
    }

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
        _trayMenu.Font = new Drawing.Font("Segoe UI", 9F);
        _trayMenu.Items.Add("打开主窗口", null, (_, _) => RestoreFromTray());
        _trayCurrentWeaponStatus = new Forms.ToolStripMenuItem { Enabled = false };
        _trayMenu.Items.Add(_trayCurrentWeaponStatus);
        _trayWeaponMenu = new Forms.ToolStripMenuItem("选择枪械");
        ConfigureTrayDropDown(_trayWeaponMenu.DropDown);
        _trayMenu.Items.Add(_trayWeaponMenu);
        _trayMenu.Items.Add(new Forms.ToolStripSeparator());
        _trayMenu.Items.Add("退出", null, (_, _) => Close());
        _trayIcon.ContextMenuStrip = _trayMenu;
        _trayIcon.DoubleClick += (_, _) => RestoreFromTray();
        _trayIcon.Visible = true;
        RefreshTrayWeaponMenu();
    }

    private void InitializeGlobalHotKeys()
    {
        _windowHandle = new WindowInteropHelper(this).Handle;
        _windowSource = HwndSource.FromHwnd(_windowHandle);
        _windowSource?.AddHook(HandleWindowMessage);

        RegisterGlobalHotKey(HotKeyXDecrease, 0x25, "Ctrl + Alt + 左方向键");
        RegisterGlobalHotKey(HotKeyXIncrease, 0x27, "Ctrl + Alt + 右方向键");
        RegisterGlobalHotKey(HotKeyYDecrease, 0x28, "Ctrl + Alt + 下方向键");
        RegisterGlobalHotKey(HotKeyYIncrease, 0x26, "Ctrl + Alt + 上方向键");
        RefreshTrayWeaponMenu();
    }

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
        var adjustment = id switch
        {
            HotKeyXDecrease => (AdjustX: true, Direction: -1),
            HotKeyXIncrease => (AdjustX: true, Direction: 1),
            HotKeyYDecrease => (AdjustX: false, Direction: -1),
            HotKeyYIncrease => (AdjustX: false, Direction: 1),
            _ => (AdjustX: true, Direction: 0)
        };
        if (adjustment.Direction != 0) ApplyHotKeyAdjustment(adjustment.AdjustX, adjustment.Direction);
        return IntPtr.Zero;
    }

    private void ApplyHotKeyAdjustment(bool adjustX, int direction)
    {
        var result = _viewModel.AdjustCurrentWeaponSensitivity(adjustX, direction);
        if (!result.IsSuccess)
        {
            QueueHotKeyNotification(result.Error ?? "当前枪械灵敏度调整失败。", Forms.ToolTipIcon.Warning);
            return;
        }

        RefreshTrayWeaponMenu();
        QueueHotKeyNotification($"{result.Weapon} 的基础 {result.Axis} 已调整为 {result.BaseValue}，增幅值保持不变。", Forms.ToolTipIcon.Info);
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

    protected override void OnClosed(EventArgs eventArgs)
    {
        _hotKeyNotificationTimer.Stop();
        UnregisterGlobalHotKeys();
        _trayMenu.Dispose();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        base.OnClosed(eventArgs);
    }

    private void UpdateLicenseStatus()
    {
        var licenseJson = LicenseStore.Load();
        var license = string.IsNullOrWhiteSpace(licenseJson) ? null : LicenseDocument.FromJson(licenseJson!);
        LicenseStatusText.Text = license?.Mode == "expires" && license.ExpiresUtc is { } expiresUtc
            ? $"授权至 {expiresUtc.ToLocalTime():yyyy-MM-dd}"
            : "永久授权";
    }

    private async void DeployEmbeddedPackage()
    {
        try
        {
            DecompressBtn.IsEnabled = false;
            ShowDeploymentProgress();
            DeploymentStatusText.Text = "正在解压资源包…";
            IProgress<PackageDeploymentProgress> progress = new Progress<PackageDeploymentProgress>(UpdateDeploymentProgress);
            var result = await Task.Run(() => ObscuredPackageDeploymentService.Deploy(progress.Report));
            LoadDefaultFilesIfAvailable();
            OpenLauncherInExplorer();
            var ghubLaunchResult = LogitechGHubLauncher.TryLaunchInstalledGHub();
            if (!ghubLaunchResult.IsLaunched)
                LogitechGHubLauncher.OpenDownloadPage();
            DeploymentStatusText.Text = result.ExtractedTargets.Count > 0
                ? "部署完成，已就绪"
                : "已检查，资源已存在";
            ShowDeploymentResult("部署完成", LogitechGHubLauncher.AppendFailureMessage($"解压成功：{result.ExtractedTargets.Count} 个文件", ghubLaunchResult));
            BeginStoryboard((System.Windows.Media.Animation.Storyboard)FindResource("DeploymentSuccessPulse"));
        }
        catch (Exception exception)
        {
            DeploymentStatusText.Text = "部署失败，请检查权限";
            ShowDeploymentResult("部署失败", exception.Message);
        }
        finally
        {
            DecompressBtn.IsEnabled = true;
        }
    }

    private void ShowDeploymentProgress()
    {
        DeploymentDialogOverlay.Visibility = Visibility.Visible;
        DeploymentDialogTitle.Text = "正在部署资源包";
        DeploymentDialogMessage.Text = "正在准备部署…";
        DeploymentProgressBar.Visibility = Visibility.Visible;
        DeploymentProgressBar.Value = 0;
        DeploymentProgressText.Visibility = Visibility.Visible;
        DeploymentProgressText.Text = "0 / 0 · 0%";
        DeploymentDialogCloseButton.Visibility = Visibility.Collapsed;
    }

    private void UpdateDeploymentProgress(PackageDeploymentProgress progress)
    {
        DeploymentProgressBar.Value = progress.Percentage;
        DeploymentProgressText.Text = $"{progress.CompletedFiles} / {progress.TotalFiles} · {progress.Percentage:0}%";
        DeploymentDialogMessage.Text = string.IsNullOrEmpty(progress.CurrentTarget)
            ? "正在准备部署…"
            : $"正在处理：{progress.CurrentTarget}";
    }

    private void ShowDeploymentResult(string title, string message)
    {
        DeploymentDialogOverlay.Visibility = Visibility.Visible;
        DeploymentDialogTitle.Text = title;
        DeploymentDialogMessage.Text = message;
        DeploymentProgressBar.Visibility = Visibility.Collapsed;
        DeploymentProgressText.Visibility = Visibility.Collapsed;
        DeploymentDialogCloseButton.Visibility = Visibility.Visible;
    }

    private void CloseDeploymentDialog()
    {
        DeploymentDialogOverlay.Visibility = Visibility.Collapsed;
    }

    private void ConfirmOpenDownloadPage()
    {
        DownloadConfirmOverlay.Visibility = Visibility.Visible;
    }

    private static void OpenLauncherInExplorer()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = "/select,\"" + ObscuredPackageDeploymentService.LauncherPath + "\"",
            UseShellExecute = true
        });
    }

    private void ConfigureTrayDropDown(Forms.ToolStripDropDown dropDown)
    {
        dropDown.Renderer = new TrayMenuRenderer(this);
        dropDown.Font = new Drawing.Font("Segoe UI", 9F);
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
        dropDown.Disposed += (_, _) => _roundedTrayMenus.Remove(dropDown);
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

    private void RefreshTrayWeaponMenu()
    {
        if (_trayWeaponMenu is null) return;

        if (_trayCurrentWeaponStatus is not null)
        {
            var weapon = string.IsNullOrWhiteSpace(_viewModel.SelectedWeapon) ? "未选择" : _viewModel.SelectedWeapon;
            _trayCurrentWeaponStatus.Text = $"当前枪械：{weapon}";
        }

        _trayWeaponMenu.DropDownItems.Clear();
        ConfigureTrayDropDown(_trayWeaponMenu.DropDown);
        if (_viewModel.Weapons.Count == 0)
        {
            _trayWeaponMenu.DropDownItems.Add(new Forms.ToolStripMenuItem("尚未加载配置") { Enabled = false });
            return;
        }

        foreach (var weapon in _viewModel.Weapons)
        {
            var weaponMenu = new Forms.ToolStripMenuItem(weapon)
            {
                Checked = string.Equals(_viewModel.SelectedWeapon, weapon, StringComparison.Ordinal)
            };
            ConfigureTrayDropDown(weaponMenu.DropDown);
            weaponMenu.DropDownItems.Add(new Forms.ToolStripMenuItem("仅选择此枪械", null, (_, _) =>
            {
                SelectWeaponFromTray(weapon);
                _trayMenu.Close();
            }));
            weaponMenu.DropDownItems.Add(new Forms.ToolStripSeparator());
            AddTrayBindingMenu(weaponMenu, weapon, "无修饰键", "qq1156777787", nameof(MainWindowViewModel.PrimaryKey));
            AddTrayBindingMenu(weaponMenu, weapon, "按住 Alt", "qq1156777787_second", nameof(MainWindowViewModel.AltKey));
            AddTrayBindingMenu(weaponMenu, weapon, "按住 Ctrl", "Third", nameof(MainWindowViewModel.CtrlKey));
            _trayWeaponMenu.DropDownItems.Add(weaponMenu);
        }
    }

    private void AddTrayBindingMenu(Forms.ToolStripMenuItem weaponMenu, string weapon, string label, string suffix, string propertyName)
    {
        var bindingMenu = new Forms.ToolStripMenuItem(label);
        ConfigureTrayDropDown(bindingMenu.DropDown);
        var currentValue = _viewModel.GetBindingValue(weapon, suffix);
        foreach (var option in KeyOptionsFor(MouseModelList.SelectedValue?.ToString(), currentValue))
        {
            var optionMenu = new Forms.ToolStripMenuItem(option.Text)
            {
                Checked = option.Value == currentValue,
                CheckOnClick = false
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
    }

    private void ApplyTrayBinding(string weapon, string propertyName, string value)
    {
        Dispatcher.BeginInvoke(new Action(() =>
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
        }));
    }

    private void LoadDefaultFilesIfAvailable()
    {
        _keyBindingsPath = ObscuredPackageDeploymentService.GetInstalledConfigurationPath("sorinkg.lua");
        _sensitivityPath = ObscuredPackageDeploymentService.GetInstalledConfigurationPath("sorinxs.lua");
        if (_keyBindingsPath is not null && _sensitivityPath is not null) LoadFiles();
        else RefreshTrayWeaponMenu();
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
        RefreshTrayWeaponMenu();
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
        RefreshTrayWeaponMenu();
    }

    private void FindWeaponByPrefix(TextCompositionEventArgs args)
    {
        if (string.IsNullOrWhiteSpace(args.Text)) return;

        _weaponSearchPrefix += args.Text;
        var matchedWeapon = WeaponList.Items.OfType<WeaponListItem>()
            .FirstOrDefault(weapon => weapon.Name.StartsWith(_weaponSearchPrefix, StringComparison.OrdinalIgnoreCase));
        if (matchedWeapon is null)
        {
            _weaponSearchPrefix = args.Text;
            matchedWeapon = WeaponList.Items.OfType<WeaponListItem>()
                .FirstOrDefault(weapon => weapon.Name.StartsWith(_weaponSearchPrefix, StringComparison.OrdinalIgnoreCase));
        }

        if (matchedWeapon is not null)
        {
            WeaponList.SelectedItem = matchedWeapon;
            WeaponList.ScrollIntoView(matchedWeapon);
        }

        _weaponSearchTimer.Stop();
        _weaponSearchTimer.Start();
        args.Handled = true;
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
        MouseModelList.SelectionChanged += (_, _) =>
        {
            RefreshKeyOptions();
            RefreshTrayWeaponMenu();
        };
        MouseModelList.SelectedIndex = 0;
    }

    private void SaveChanges()
    {
        try
        {
            _viewModel.Save();
            SaveBtn.Content = "应用成功";
            RefreshWeaponList(_viewModel.SelectedWeapon);
            RefreshTrayWeaponMenu();
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
        using var iconStream = typeof(MainWindow).Assembly.GetManifestResourceStream("AMacQConfigEditor.Resources.AMacQ.ico");
        if (iconStream is null) return;

        var icon = System.Windows.Media.Imaging.BitmapFrame.Create(
            iconStream,
            System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat,
            System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
        icon.Freeze();
        Icon = icon;
        TitleBarIcon.Source = icon;
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
    private sealed record KeyOption(string Text, string? Value);
    private sealed record SelectionOption(string Text, string Value);

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
            var bounds = eventArgs.AffectedBounds;
            bounds.Width -= 1;
            bounds.Height -= 1;
            using var path = CreateRoundedRectanglePath(bounds, TrayMenuCornerRadius);
            using var pen = new Drawing.Pen(_window.GetThemeColor("BorderPanelColor"));
            eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            eventArgs.Graphics.DrawPath(pen, path);
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
