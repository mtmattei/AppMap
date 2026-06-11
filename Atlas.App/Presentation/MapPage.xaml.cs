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

    private void ZoomFit_Click(object sender, RoutedEventArgs e) => MapZoom.FitToCanvas();

    private void ResetView_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        MapZoom.ResetViewport();
        args.Handled = true;
    }

    private void FitView_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        MapZoom.FitToCanvas();
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

    private void ZoomBy(double factor) =>
        MapZoom.ZoomLevel = Math.Clamp(MapZoom.ZoomLevel * factor, MapZoom.MinZoomLevel, MapZoom.MaxZoomLevel);

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
            CanvasHost.CapturePointer(e.Pointer);
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

    // ----- double-tap focuses a node: zoom to 1:1 and center it -----

    private void NodeCard_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not Atlas.Core.AppNode { Position: { } position })
        {
            return;
        }

        var zoom = Math.Max(MapZoom.ZoomLevel, 1.0);
        MapZoom.ZoomLevel = zoom;
        MapZoom.HorizontalScrollValue = (position.X + 91) * zoom - MapZoom.ActualWidth / 2;
        MapZoom.VerticalScrollValue = (position.Y + 39) * zoom - MapZoom.ActualHeight / 2;
        e.Handled = true;
    }
}
