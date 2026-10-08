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

public partial class MainWindow : Window, IWeaponQuickSwitchHost
{
    private readonly MainWindowViewModel _viewModel = new();
    private readonly SensitivityOverlayWindow _sensitivityOverlay = new();
    private readonly Dictionary<string, Control> _fieldInputs = [];
    private string? _keyBindingsPath;
    private string? _sensitivityPath;
    private readonly DispatcherTimer _saveResetTimer = new() { Interval = TimeSpan.FromSeconds(1.5) };
    private readonly DispatcherTimer _hotKeyNotificationTimer = new() { Interval = TimeSpan.FromMilliseconds(1500) };
    private readonly Forms.NotifyIcon _trayIcon = new();
    private const int WmHotKey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModNoRepeat = 0x4000;
    private const int VkControl = 0x11;
    private const int VkMenu = 0x12;
    // 左右修饰键分开检测：按住任意一侧都应识别为对应的槽位。
    private const int VirtualKeyControl = 0xA2;
    private const int VirtualKeyRControl = 0xA3;
    private const int VirtualKeyMenu = 0xA4;
    private const int VirtualKeyRMenu = 0xA5;
    private const int HotKeyXDecrease = 1;
    private const int HotKeyXIncrease = 2;
    private const int HotKeyYDecrease = 3;
    private const int HotKeyYIncrease = 4;
    private const int HotKeyWeaponMenu = 6;
    private const int HotKeyYFastDecrease = 7;
    private const int HotKeyYFastIncrease = 8;
    private const int TrayMenuCornerRadius = 10;
    // 托盘菜单通常从屏幕右下角弹出。固定子菜单向左展开，保证多级菜单始终
    // 沿同一方向排列，鼠标移动到更深层菜单时不会穿过上一级菜单。
    private const Forms.ToolStripDropDownDirection TraySubMenuDirection = Forms.ToolStripDropDownDirection.Left;
    private readonly Forms.ContextMenuStrip _trayMenu = new();
    private readonly HashSet<Forms.ToolStripDropDown> _roundedTrayMenus = [];
    private readonly HashSet<Forms.ToolStripDropDown> _configuredTrayMenus = [];
    private readonly List<Forms.ToolStripItem> _trayWeaponMenuItems = [];
    private Forms.ToolStripMenuItem? _trayCurrentWeaponStatus;
    private Forms.ToolStripMenuItem? _traySensitivityStatus;
    private bool _trayMenuDirty = true;
    private bool _keepTrayMenuOpenForSensitivity;
    private HwndSource? _windowSource;
    private IntPtr _windowHandle;
    private readonly HashSet<int> _registeredHotKeys = [];
    private QuickSwitchWindow? _quickSwitchWindow;

    /// <summary>后台等待按键绑定的最长时间；超时后静默取消，不打扰用户。</summary>
    private static readonly TimeSpan BindingCaptureTimeout = TimeSpan.FromSeconds(15);

    /// <summary>"等待按键…"提示的停留时长：只需让用户知道程序在监听，不必挂满整个等待窗口。</summary>
    private static readonly TimeSpan BindingWaitingHintDuration = TimeSpan.FromSeconds(2.5);
    private static readonly System.Windows.Media.Color BindingSuccessColor =
        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF5DD7FF")!;
    private static readonly System.Windows.Media.Color BindingFailureColor =
        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFFFB45B")!;
    private static readonly System.Windows.Media.Color BindingWaitingColor =
        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFB9CAE0")!;
    private readonly DispatcherTimer _bindingCaptureTimer = new() { Interval = BindingCaptureTimeout };
    private MouseSideButtonListener? _bindingCaptureMouse;
    private DigitKeyListener? _bindingCaptureKeyboard;
    private (string Weapon, WeaponBindingSlot Slot)? _bindingCaptureTarget;

    private WeaponListItem[] _allWeaponItems = [];
    private string? _pendingHotKeyNotification;
    private Forms.ToolTipIcon _pendingHotKeyNotificationIcon = Forms.ToolTipIcon.Info;
    private IntPtr _trayOutsideClickHook;
    private WinEventDelegate? _trayOutsideClickHookProc;
    private const uint EventSystemCaptureStart = 0x0008;
    private const uint WineventOutOfContext = 0x0000;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(Drawing.Point point);

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

    private const uint GaRoot = 2;

    private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => InitializeGlobalHotKeys();
        TechnologyThemeService.ApplyCurrentTheme(this);
        DataContext = _viewModel;
        SetWindowIcon();
        ConfigureTrayIcon();
        UpdateLicenseStatus();

        // 启动时恢复（启用）配置文件：若上一次退出时被禁用，这里要改回来。
        var restoreFailures = ObscuredPackageDeploymentService.RestoreRuntimeConfigurationFiles();
        if (restoreFailures.Count > 0) WarnRuntimeConfigurationFailure(restoreFailures, disabling: false);

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
        DownloadConfirmAcknowledgeButton.Click += (_, _) =>
        {
            DownloadConfirmOverlay.Visibility = Visibility.Collapsed;
            DeployEmbeddedPackage();
        };
        HelpBtn.Click += (_, _) => new HelpWindow(this).ShowDialog();
        SaveBtn.Click += (_, _) => SaveChanges();
        MinimizeBtn.Click += (_, _) => HideToTray();
        CloseBtn.Click += (_, _) => Close();
        Closing += (_, _) =>
        {
            _viewModel.FlushPendingSensitivityWrite();

            // 关闭后禁用配置文件，使鼠标宏无法再读取它们；失败必须让用户知道，否则保护形同虚设。
            var disableFailures = ObscuredPackageDeploymentService.DisableRuntimeConfigurationFiles();
            if (disableFailures.Count > 0) WarnRuntimeConfigurationFailure(disableFailures, disabling: true);
        };
        StateChanged += (_, _) => { if (WindowState == WindowState.Minimized) HideToTray(); };
        WeaponList.SelectionChanged += (_, _) => OnWeaponSelectionChanged();
        WeaponList.ItemContainerStyle = (Style)FindResource("WeaponListItem");
        WeaponSearchBox.TextChanged += HandleWeaponSearchTextChanged;
        WeaponSearchBox.PreviewKeyDown += HandleWeaponSearchKeyDown;
        WeaponSearchBox.GotKeyboardFocus += FocusWeaponSearch;
        WeaponSearchBox.LostKeyboardFocus += BlurWeaponSearch;

        BuildFieldCards();
        PopulateGlobalOptions();
        LoadDefaultFilesIfAvailable();
        _saveResetTimer.Tick += (_, _) => { SaveBtn.Content = "应用"; _saveResetTimer.Stop(); };
        _hotKeyNotificationTimer.Tick += (_, _) => FlushHotKeyNotification();
    }

    protected override void OnClosed(EventArgs eventArgs)
    {
        _hotKeyNotificationTimer.Stop();
        UnregisterGlobalHotKeys();
        if (_quickSwitchWindow is not null)
        {
            _quickSwitchWindow.Close();
            _quickSwitchWindow = null;
        }
        CancelBindingCapture();
        _sensitivityOverlay.CloseOverlay();
        _trayMenu.Dispose();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        base.OnClosed(eventArgs);
    }

    private void UpdateLicenseStatus()    {
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
            var ghubLaunchResult = LogitechGHubLauncher.TryLaunchInstalledGHub();
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

    /// <summary>
    /// 配置文件改名失败时的提示。
    ///
    /// 禁用失败意味着关闭程序后鼠标宏仍可能可用（保护失效），必须用模态框明确告知；
    /// 恢复失败只在托盘气泡里提示，避免启动阶段弹窗打断用户。
    /// </summary>
    private void WarnRuntimeConfigurationFailure(IReadOnlyList<string> fileNames, bool disabling)
    {
        var files = string.Join("、", fileNames);
        if (disabling)
        {
            MessageBox.Show(
                $"无法禁用配置文件：{files}。\n可能正被罗技 G HUB 或游戏占用。\n关闭程序后鼠标宏仍可能可用，请关闭 G HUB 后重试。",
                "禁用失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _trayIcon.ShowBalloonTip(3000, "AMacQ", $"无法恢复配置文件：{files}，可能正被其他程序占用。", Forms.ToolTipIcon.Warning);
    }

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



}


