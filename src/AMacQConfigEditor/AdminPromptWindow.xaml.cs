using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace AMacQConfigEditor;

public partial class AdminPromptWindow : Window
{
    public AdminPromptWindow()
    {
        InitializeComponent();
        RestartButton.Click += (_, _) => DialogResult = true;
        ContinueButton.Click += (_, _) => DialogResult = false;
        CloseButton.Click += (_, _) => DialogResult = false;
        Loaded += (_, _) => UpdateWindowClip();
        SizeChanged += (_, _) => UpdateWindowClip();
    }

    public void ShowRestartFailure()
    {
        StatusText.Text = "未能以管理员权限重新启动，程序将关闭。";
        StatusText.Visibility = Visibility.Visible;
        RestartButton.IsEnabled = false;
        ContinueButton.Focus();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    private void UpdateWindowClip()
    {
        WindowShell.Clip = new RectangleGeometry(new Rect(0, 0, WindowShell.ActualWidth, WindowShell.ActualHeight), 12, 12);
    }
}
