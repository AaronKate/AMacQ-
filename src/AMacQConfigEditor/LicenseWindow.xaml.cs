using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using AMacQConfigEditor.Licensing;
using AMacQConfigEditor.Services;

namespace AMacQConfigEditor;

public partial class LicenseWindow : Window
{
    public LicenseWindow()
    {
        InitializeComponent();
        TechnologyThemeService.ApplyRandomTheme(this);
        SetWindowIcon();
        MachineCodeBox.Text = MachineCodeService.CurrentMachineCode;
        StatusText.Text = "请将机器码发送给授权方，然后导入返回的许可证文件。";
        ImportButton.Click += (_, _) => ImportLicense();
        CopyMachineCodeButton.Click += (_, _) => CopyMachineCode();
        CloseButton.Click += (_, _) => Close();
        Loaded += (_, _) => UpdateWindowClip();
        SizeChanged += (_, _) => UpdateWindowClip();
    }

    private void CopyMachineCode()
    {
        Clipboard.SetText(MachineCodeBox.Text);
        CopyMachineCodeButton.Content = "已复制";
        var resetTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        resetTimer.Tick += (_, _) => { CopyMachineCodeButton.Content = "复制机器码"; resetTimer.Stop(); };
        resetTimer.Start();
    }

    private void SetWindowIcon()
    {
        using var iconStream = typeof(LicenseWindow).Assembly.GetManifestResourceStream("AMacQConfigEditor.Resources.AMacQ.ico");
        if (iconStream is null) return;

        var icon = BitmapFrame.Create(iconStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        icon.Freeze();
        Icon = icon;
        TitleBarIcon.Source = icon;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    private void UpdateWindowClip()
    {
        WindowShell.Clip = new RectangleGeometry(new Rect(0, 0, WindowShell.ActualWidth, WindowShell.ActualHeight), 12, 12);
    }

    private void ImportLicense()
    {
        var dialog = new OpenFileDialog { Filter = "许可证文件 (*.json)|*.json|所有文件 (*.*)|*.*" };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            var license = System.IO.File.ReadAllText(dialog.FileName);
            var result = LicenseValidator.Validate(license, MachineCodeService.CurrentMachineCode, DateTime.UtcNow, LicenseValidator.PublicKeyXml);
            if (!result.IsValid)
            {
                StatusText.Text = result.Error;
                StatusText.Foreground = new SolidColorBrush(Color.FromRgb(251, 113, 133));
                return;
            }

            LicenseStore.Import(dialog.FileName);
            DialogResult = true;
        }
        catch (Exception)
        {
            StatusText.Text = "无法导入许可证文件。";
            StatusText.Foreground = new SolidColorBrush(Color.FromRgb(251, 113, 133));
        }
    }
}
