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
    private readonly DispatcherTimer _hideTimer = new() { Interval = TimeSpan.FromSeconds(1.8) };
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

    public void ShowWeaponSensitivity(string weapon, string sensitivityX, string sensitivityY)
    {
        _showVersion++;
        DisplayText.Text = $"{weapon} | x: {sensitivityX}  y: {sensitivityY}";

        _hideTimer.Stop();
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
