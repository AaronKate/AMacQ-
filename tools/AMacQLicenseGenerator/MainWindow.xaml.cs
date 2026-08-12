using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls;
using Microsoft.Win32;

namespace AMacQLicenseGenerator;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        SetPrivateKeyPath(FindPrivateKeyPath());
        ExpiryDateBox.Text = DateTime.Today.AddYears(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        ExpiryCalendar.SelectedDate = DateTime.Today.AddYears(1);
        ApplyRandomTheme();
        SetLicenseMode(false);
    }

    private static string FindPrivateKeyPath()
    {
        var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "AMacQLicense.private.xml");
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AMacQLicense.private.xml");
    }

    private void SetPrivateKeyPath(string path)
    {
        PrivateKeyDisplay.Text = Path.GetFileName(path);
        PrivateKeyDisplay.Tag = path;
    }

    private string PrivateKeyPath => PrivateKeyDisplay.Tag as string ?? FindPrivateKeyPath();

    private void ChoosePrivateKey_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "RSA 私钥 (*.xml)|*.xml|所有文件 (*.*)|*.*" };
        if (dialog.ShowDialog(this) == true) SetPrivateKeyPath(dialog.FileName);
    }

    private void PerpetualButton_Click(object sender, RoutedEventArgs e) => SetLicenseMode(false);
    private void ExpiryButton_Click(object sender, RoutedEventArgs e) => SetLicenseMode(true);

    private void SetLicenseMode(bool isExpiryMode)
    {
        ExpiryDateArea.Visibility = isExpiryMode ? Visibility.Visible : Visibility.Collapsed;
        ExpiryLabel.Visibility = isExpiryMode ? Visibility.Visible : Visibility.Collapsed;
        PerpetualButton.Background = isExpiryMode ? (Brush)FindResource("InputBrush") : (Brush)FindResource("AccentBrush");
        PerpetualButton.BorderThickness = isExpiryMode ? new Thickness(1) : new Thickness(0);
        ExpiryButton.Background = isExpiryMode ? (Brush)FindResource("AccentBrush") : (Brush)FindResource("InputBrush");
        ExpiryButton.BorderThickness = isExpiryMode ? new Thickness(0) : new Thickness(1);
        StatusText.Text = isExpiryMode ? "许可证将在所填日期结束时失效" : "永久授权不会自动失效";
    }

    private void ShowCalendar_Click(object sender, RoutedEventArgs e)
    {
        if (DateTime.TryParseExact(ExpiryDateBox.Text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var selectedDate))
            ExpiryCalendar.SelectedDate = selectedDate;
        CalendarPopup.IsOpen = true;
    }

    private void ExpiryCalendar_SelectedDatesChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ExpiryCalendar.SelectedDate is not { } selectedDate) return;
        ExpiryDateBox.Text = selectedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        CalendarPopup.IsOpen = false;
    }

    private void Generate_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "许可证文件 (*.json)|*.json", FileName = "AMacQ-license.json", AddExtension = true };
        if (dialog.ShowDialog(this) != true) return;
        var isExpiryMode = ExpiryDateArea.Visibility == Visibility.Visible;
        DateTime? expires = null;
        DateTime expiryDate = default;
        if (isExpiryMode && !DateTime.TryParseExact(ExpiryDateBox.Text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out expiryDate))
        {
            StatusText.Text = "到期日期请使用 yyyy-MM-dd 格式。";
            return;
        }
        if (isExpiryMode) expires = expiryDate;
        var expiresUtc = expires is null ? (DateTime?)null : DateTime.SpecifyKind(expires.Value.Date.AddDays(1).AddMilliseconds(-1), DateTimeKind.Utc);
        var mode = isExpiryMode ? "expires" : "perpetual";
        if (LicenseGenerator.TryGenerate(PrivateKeyPath, dialog.FileName, MachineCodeBox.Text, mode, expiresUtc, out var message))
        {
            StatusText.Text = message;
            MessageBox.Show(message, "授权签发成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else { StatusText.Text = message; MessageBox.Show(message, "授权签发失败", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (e.ChangedButton == MouseButton.Left) DragMove(); }

    private void ApplyRandomTheme()
    {
        var themes = new[] { new[] { "#101B36", "#050914", "#172A56", "#0B1430", "#5EB5FF", "#5672FF", "#172B54", "#080E20", "#142542", "#081124", "#5278AF", "#92D2FF", "#213E6B", "#142D52" }, new[] { "#0D352F", "#061916", "#165348", "#0B2D27", "#44F3C4", "#22B890", "#10382F", "#071C19", "#0D332C", "#061C18", "#3A9C86", "#78FFD9", "#185B4C", "#0E3D34" }, new[] { "#251B4B", "#0E0A26", "#3D2D70", "#191037", "#D48CFF", "#7B8CFF", "#302550", "#120D29", "#2A214A", "#120C28", "#8864B8", "#DEA8FF", "#513A82", "#37265F" }, new[] { "#3B2118", "#180D09", "#60351F", "#2C180E", "#FFB06B", "#E37C4B", "#4B2A1D", "#1E100B", "#432619", "#21120C", "#B27A50", "#FFD09A", "#7B452A", "#572F1C" } };
        var keys = new[] { "AppStart", "AppEnd", "TitleStart", "TitleEnd", "AccentA", "AccentB", "PanelStart", "PanelEnd", "InputStart", "InputEnd", "Border", "Focus", "Hover", "Pressed" };
        var palette = themes[new Random().Next(themes.Length)];
        for (var i = 0; i < keys.Length; i++) Resources[keys[i]] = (Color)ColorConverter.ConvertFromString(palette[i])!;
    }
}
