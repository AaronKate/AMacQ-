using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;

namespace AMacQConfigEditor;

public partial class HelpWindow : Window
{
    private Point? _guideImageDragStart;
    private double _guideImageDragHorizontalOffset;
    private double _guideImageDragVerticalOffset;
    private double _guideImageBaseWidth;
    private double _guideImageBaseHeight;
    private double _guideImageZoom = 1;

    public HelpWindow(MainWindow owner)
    {
        InitializeComponent();
        Owner = owner;
        InheritTheme(owner);
        HelpWindowCloseButton.Click += (_, _) => Close();
        GuideImageCloseButton.Click += (_, _) => CloseGuideImage();
    }

    private void InheritTheme(Window owner)
    {
        foreach (var key in owner.Resources.Keys)
        {
            if (owner.Resources[key] is Color color)
            {
                Resources[key] = color;
            }
        }
    }

    private void OpenExternalLink(object sender, RequestNavigateEventArgs eventArgs)
    {
        Process.Start(new ProcessStartInfo(eventArgs.Uri.AbsoluteUri) { UseShellExecute = true });
        eventArgs.Handled = true;
    }

    private void OpenGuideImage(object sender, MouseButtonEventArgs eventArgs)
    {
        if (sender is not Image image || image.Source is null) return;

        GuideImagePreview.Source = image.Source;
        GuideImageOverlay.Visibility = Visibility.Visible;
        GuideImageScrollViewer.UpdateLayout();
        FitGuideImageToPreview();
        eventArgs.Handled = true;
    }

    private void CloseGuideImageWhenBackgroundClicked(object sender, MouseButtonEventArgs eventArgs)
    {
        if (ReferenceEquals(eventArgs.OriginalSource, GuideImageOverlay)) CloseGuideImage();
    }

    private void CloseGuideImage()
    {
        EndGuideImageDrag();
        GuideImageOverlay.Visibility = Visibility.Collapsed;
        GuideImagePreview.Source = null;
        _guideImageBaseWidth = 0;
        _guideImageBaseHeight = 0;
        _guideImageZoom = 1;
    }

    private void BeginGuideImageDrag(object sender, MouseButtonEventArgs eventArgs)
    {
        _guideImageDragStart = eventArgs.GetPosition(GuideImageScrollViewer);
        _guideImageDragHorizontalOffset = GuideImageScrollViewer.HorizontalOffset;
        _guideImageDragVerticalOffset = GuideImageScrollViewer.VerticalOffset;
        GuideImagePreview.CaptureMouse();
        GuideImagePreview.Cursor = Cursors.SizeAll;
        eventArgs.Handled = true;
    }

    private void DragGuideImage(object sender, MouseEventArgs eventArgs)
    {
        if (_guideImageDragStart is null || eventArgs.LeftButton != MouseButtonState.Pressed) return;

        var currentPosition = eventArgs.GetPosition(GuideImageScrollViewer);
        GuideImageScrollViewer.ScrollToHorizontalOffset(_guideImageDragHorizontalOffset - (currentPosition.X - _guideImageDragStart.Value.X));
        GuideImageScrollViewer.ScrollToVerticalOffset(_guideImageDragVerticalOffset - (currentPosition.Y - _guideImageDragStart.Value.Y));
    }

    private void EndGuideImageDrag(object? sender = null, MouseButtonEventArgs? eventArgs = null)
    {
        _guideImageDragStart = null;
        if (GuideImagePreview.IsMouseCaptured) GuideImagePreview.ReleaseMouseCapture();
        GuideImagePreview.Cursor = Cursors.Hand;
        if (eventArgs is not null) eventArgs.Handled = true;
    }

    private void FitGuideImageToPreview()
    {
        if (GuideImagePreview.Source is null) return;

        var availableWidth = GuideImageScrollViewer.ViewportWidth;
        var availableHeight = GuideImageScrollViewer.ViewportHeight;
        if (availableWidth <= 0 || availableHeight <= 0) return;

        var scale = Math.Min(1, Math.Min(availableWidth / GuideImagePreview.Source.Width, availableHeight / GuideImagePreview.Source.Height));
        _guideImageBaseWidth = GuideImagePreview.Source.Width * scale;
        _guideImageBaseHeight = GuideImagePreview.Source.Height * scale;
        _guideImageZoom = 1;
        UpdateGuideImageSize();
        GuideImageScrollViewer.ScrollToHome();
    }

    private void ZoomGuideImage(object sender, MouseWheelEventArgs eventArgs)
    {
        if (GuideImagePreview.Source is null || _guideImageBaseWidth <= 0 || _guideImageBaseHeight <= 0) return;

        var zoomFactor = eventArgs.Delta > 0 ? 1.15 : 1 / 1.15;
        var nextZoom = Math.Max(1, Math.Min(4, _guideImageZoom * zoomFactor));
        if (Math.Abs(nextZoom - _guideImageZoom) < 0.001) return;

        var pointer = eventArgs.GetPosition(GuideImagePreview);
        var horizontalRatio = (GuideImageScrollViewer.HorizontalOffset + pointer.X) / GuideImagePreview.ActualWidth;
        var verticalRatio = (GuideImageScrollViewer.VerticalOffset + pointer.Y) / GuideImagePreview.ActualHeight;
        _guideImageZoom = nextZoom;
        UpdateGuideImageSize();
        GuideImageScrollViewer.ScrollToHorizontalOffset(horizontalRatio * GuideImagePreview.Width - pointer.X);
        GuideImageScrollViewer.ScrollToVerticalOffset(verticalRatio * GuideImagePreview.Height - pointer.Y);
        eventArgs.Handled = true;
    }

    private void UpdateGuideImageSize()
    {
        GuideImagePreview.Width = _guideImageBaseWidth * _guideImageZoom;
        GuideImagePreview.Height = _guideImageBaseHeight * _guideImageZoom;
    }
}
