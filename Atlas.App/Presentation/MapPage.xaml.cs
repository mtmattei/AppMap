using Microsoft.UI.Xaml.Input;

namespace Atlas.App.Presentation;

public sealed partial class MapPage : Page
{
    public MapPage()
    {
        this.InitializeComponent();
    }

    private void ZoomIn_Click(object sender, RoutedEventArgs e) => ZoomBy(1.2);

    private void ZoomOut_Click(object sender, RoutedEventArgs e) => ZoomBy(1 / 1.2);

    private void ZoomFit_Click(object sender, RoutedEventArgs e) => FitAndCenter();

    // FitToCanvas only sets the zoom level; a leftover pan offset (e.g. after a window
    // resize) keeps the map shifted, so recenter explicitly.
    private void FitAndCenter()
    {
        StopViewAnimation();
        MapZoom.FitToCanvas();
        MapZoom.CenterContent();
    }

    private void ResetView_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        MapZoom.ResetViewport();
        args.Handled = true;
    }

    private void FitView_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        FitAndCenter();
        args.Handled = true;
    }

    private void ZoomInKey_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        ZoomBy(1.2);
        args.Handled = true;
    }

    private void ZoomOutKey_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        ZoomBy(1 / 1.2);
        args.Handled = true;
    }

    // ----- animated view changes (zoom buttons, Ctrl+±, focus-on-click pan) -----
    // A storyboard holds the animated DP after completing, which would mask the direct
    // writes from Ctrl+wheel, FitToCanvas, drag-pan, and double-tap. So: write targets
    // as local values (silently updates the base under the hold), then Stop to release.

    private Microsoft.UI.Xaml.Media.Animation.Storyboard? _viewAnimation;

    private void ZoomBy(double factor)
    {
        var target = Math.Clamp(MapZoom.ZoomLevel * factor, MapZoom.MinZoomLevel, MapZoom.MaxZoomLevel);
        AnimateView(("ZoomLevel", target));
    }

    private void AnimateView(params (string Property, double To)[] targets)
    {
        StopViewAnimation();

        var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
        foreach (var (property, to) in targets)
        {
            var animation = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                To = to,
                Duration = new Duration(TimeSpan.FromMilliseconds(160)),
                EnableDependentAnimation = true,
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase
                {
                    EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut,
                },
            };
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(animation, MapZoom);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(animation, property);
            storyboard.Children.Add(animation);
        }

        storyboard.Completed += (_, _) =>
        {
            if (ReferenceEquals(_viewAnimation, storyboard))
            {
                foreach (var (property, to) in targets)
                {
                    SetViewProperty(property, to);
                }
                ReleaseViewAnimation();
            }
        };
        _viewAnimation = storyboard;
        storyboard.Begin();
    }

    private void StopViewAnimation()
    {
        if (_viewAnimation is not null)
        {
            // Freeze every animatable view property at its current value before releasing the hold.
            MapZoom.ZoomLevel = MapZoom.ZoomLevel;
            MapZoom.HorizontalScrollValue = MapZoom.HorizontalScrollValue;
            MapZoom.VerticalScrollValue = MapZoom.VerticalScrollValue;
            ReleaseViewAnimation();
        }
    }

    private void ReleaseViewAnimation()
    {
        _viewAnimation?.Stop();
        _viewAnimation = null;
    }

    private void SetViewProperty(string property, double value)
    {
        switch (property)
        {
            case "ZoomLevel": MapZoom.ZoomLevel = value; break;
            case "HorizontalScrollValue": MapZoom.HorizontalScrollValue = value; break;
            case "VerticalScrollValue": MapZoom.VerticalScrollValue = value; break;
        }
    }

    // Entrance fade for blocks that materialize from a feed (agent result, scoped context).
    private void Reveal_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not UIElement element)
        {
            return;
        }

        var animation = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = new Duration(TimeSpan.FromMilliseconds(250)),
        };
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(animation, element);
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(animation, "Opacity");
        new Microsoft.UI.Xaml.Media.Animation.Storyboard { Children = { animation } }.Begin();
    }

    // View-level clipboard hand-off; Click keeps it on the UI thread, which Clipboard requires.
    private async void CopyContext_Click(object sender, RoutedEventArgs e)
    {
        var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
        package.SetText(ScopedContextText.Text);
        Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);

        if (sender is Button button)
        {
            var original = button.Content;
            button.Content = "Copied ✓";
            await Task.Delay(1400);
            button.Content = original;
        }
    }

    // ----- left-drag panning on the canvas background -----
    // Node cards, buttons, and toggles capture their own pointers, so any drag
    // still reaching the host grid is a pan gesture.

    private bool _panPressed;
    private bool _panning;
    private Windows.Foundation.Point _panStart;
    private (double H, double V) _panOrigin;

    private void CanvasHost_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!e.GetCurrentPoint(CanvasHost).Properties.IsLeftButtonPressed)
        {
            return;
        }

        // No capture yet: a press might be a tap on a card. Pan starts on movement.
        _panPressed = true;
        _panning = false;
        _panStart = e.GetCurrentPoint(CanvasHost).Position;
        _panOrigin = (MapZoom.HorizontalScrollValue, MapZoom.VerticalScrollValue);
    }

    private void CanvasHost_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_panPressed)
        {
            return;
        }

        var position = e.GetCurrentPoint(CanvasHost).Position;
        var dx = _panStart.X - position.X;
        var dy = _panStart.Y - position.Y;

        if (!_panning && (Math.Abs(dx) > 5 || Math.Abs(dy) > 5))
        {
            _panning = true;
            StopViewAnimation();
            CanvasHost.CapturePointer(e.Pointer);
            _panOrigin = (MapZoom.HorizontalScrollValue - dx, MapZoom.VerticalScrollValue - dy);
        }

        if (_panning)
        {
            MapZoom.HorizontalScrollValue = _panOrigin.H + dx;
            MapZoom.VerticalScrollValue = _panOrigin.V + dy;
            e.Handled = true;
        }
    }

    private void CanvasHost_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _panPressed = false;
        _panning = false;
        CanvasHost.ReleasePointerCaptures();
    }

    private void CanvasHost_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        _panPressed = false;
        _panning = false;
    }

    // ----- live edge re-routing while a node is dragged -----

    private Controls.EdgeLayer? _edgeLayer;

    private void NodeCard_DragDelta(object? sender, NodeMove move)
    {
        if (_edgeLayer is null || _edgeLayer.XamlRoot is null)
        {
            _edgeLayer = FindDescendant<Controls.EdgeLayer>(MapZoom);
        }

        _edgeLayer?.PreviewNodePosition(move.NodeId, new Atlas.Core.Point(move.X, move.Y));
    }

    private static T? FindDescendant<T>(DependencyObject root) where T : class
    {
        for (var i = 0; i < Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(root, i);
            if (child is T match)
            {
                return match;
            }
            if (FindDescendant<T>(child) is { } nested)
            {
                return nested;
            }
        }
        return null;
    }

    // ----- single tap focuses a node: pan it fully into view (minimal movement) -----
    // Selection opens the inspector; the pan keeps the selected card on screen so the
    // two stay visibly connected. Already-visible nodes don't move.

    private void NodeCard_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not Atlas.Core.AppNode { Position: { } position })
        {
            return;
        }

        const double margin = 28;
        const double nodeWidth = 182;
        const double nodeHeight = 96; // card plus the badge overhang above it

        var zoom = MapZoom.ZoomLevel;
        var left = position.X * zoom - MapZoom.HorizontalScrollValue;
        var top = (position.Y - 9) * zoom - MapZoom.VerticalScrollValue;
        var right = left + nodeWidth * zoom;
        var bottom = top + nodeHeight * zoom;

        var dx = left < margin ? left - margin
            : right > MapZoom.ActualWidth - margin ? right - (MapZoom.ActualWidth - margin) : 0;
        var dy = top < margin ? top - margin
            : bottom > MapZoom.ActualHeight - margin ? bottom - (MapZoom.ActualHeight - margin) : 0;

        if (Math.Abs(dx) < 1 && Math.Abs(dy) < 1)
        {
            return;
        }

        AnimateView(
            ("HorizontalScrollValue", MapZoom.HorizontalScrollValue + dx),
            ("VerticalScrollValue", MapZoom.VerticalScrollValue + dy));
    }

    // ----- double-tap focuses a node: zoom to 1:1 and center it -----

    private void NodeCard_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not Atlas.Core.AppNode { Position: { } position })
        {
            return;
        }

        StopViewAnimation();
        var zoom = Math.Max(MapZoom.ZoomLevel, 1.0);
        MapZoom.ZoomLevel = zoom;
        MapZoom.HorizontalScrollValue = (position.X + 91) * zoom - MapZoom.ActualWidth / 2;
        MapZoom.VerticalScrollValue = (position.Y + 39) * zoom - MapZoom.ActualHeight / 2;
        e.Handled = true;
    }
}
