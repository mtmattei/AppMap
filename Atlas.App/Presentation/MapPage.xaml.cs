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
}
