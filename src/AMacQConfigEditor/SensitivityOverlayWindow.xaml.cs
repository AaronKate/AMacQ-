using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace AMacQConfigEditor;

public partial class SensitivityOverlayWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;
    private const double ScreenMargin = 24;

    /// <summary>默认文字颜色，与 XAML 中原本写死的绿色保持一致。</summary>
    private static readonly Color DefaultAccentColor = (Color)ColorConverter.ConvertFromString("#FF39FF88")!;

    /// <summary>默认停留时长；调用方可以按提示的性质覆盖它。</summary>
    private static readonly TimeSpan DefaultDisplayDuration = TimeSpan.FromSeconds(1.8);

    private readonly DispatcherTimer _hideTimer = new() { Interval = DefaultDisplayDuration };
    private int _showVersion;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int index, int value);

    public SensitivityOverlayWindow()
    {
        InitializeComponent();
        _hideTimer.Tick += (_, _) => HideWithAnimation();
    }

    public void ShowWeaponSensitivity(string weapon, string sensitivityX, string sensitivityY) =>
        ShowMessage($"{weapon} | x: {sensitivityX}  y: {sensitivityY}");

    /// <summary>
    /// 显示一条短提示。除灵敏度数值外，绑键结果、等待按键等一次性反馈也走这里，
    /// 保证屏幕上的提示样式一致；<paramref name="duration"/> 可覆盖默认停留时长。
    /// </summary>
    public void ShowMessage(string message, Color? accent = null, TimeSpan? duration = null)
    {
        _showVersion++;
        DisplayText.Text = message;
        DisplayText.Foreground = new SolidColorBrush(accent ?? DefaultAccentColor);

        _hideTimer.Stop();
        _hideTimer.Interval = duration ?? DefaultDisplayDuration;
        if (!IsVisible) Show();

        UpdateLayout();
        PositionAtTopRight();
        BeginAnimation(OpacityProperty, CreateOpacityAnimation(0, 1, 120));
        _hideTimer.Start();
    }

    public void CloseOverlay()
    {
        _hideTimer.Stop();
        Close();
    }

    protected override void OnSourceInitialized(EventArgs eventArgs)
    {
        base.OnSourceInitialized(eventArgs);
        var handle = new WindowInteropHelper(this).Handle;
        var extendedStyle = GetWindowLong(handle, GwlExStyle);
        SetWindowLong(
            handle,
            GwlExStyle,
            extendedStyle | WsExTransparent | WsExToolWindow | WsExNoActivate);
    }

    private void PositionAtTopRight()
    {
        var screen = Forms.Screen.FromPoint(Forms.Cursor.Position);
        var source = PresentationSource.FromVisual(this);
        var transform = source?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        var topLeft = transform.Transform(new Point(screen.WorkingArea.Left, screen.WorkingArea.Top));
        var bottomRight = transform.Transform(new Point(screen.WorkingArea.Right, screen.WorkingArea.Bottom));

        Left = bottomRight.X - ActualWidth - ScreenMargin;
        Top = topLeft.Y + ScreenMargin;
    }

    private void HideWithAnimation()
    {
        _hideTimer.Stop();
        var version = _showVersion;
        var animation = CreateOpacityAnimation(1, 0, 220);
        animation.Completed += (_, _) =>
        {
            if (version == _showVersion) Hide();
        };
        BeginAnimation(OpacityProperty, animation);
    }

    private static DoubleAnimation CreateOpacityAnimation(double from, double to, int milliseconds) =>
        new(from, to, TimeSpan.FromMilliseconds(milliseconds))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
}
