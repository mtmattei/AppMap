namespace Atlas.App.Presentation;

public sealed partial class MapPage : Page
{
    public MapPage()
    {
        this.InitializeComponent();
    }

    private void ZoomIn_Click(object sender, RoutedEventArgs e) =>
        MapZoom.ZoomLevel = Math.Min(MapZoom.MaxZoomLevel, MapZoom.ZoomLevel * 1.2);

    private void ZoomOut_Click(object sender, RoutedEventArgs e) =>
        MapZoom.ZoomLevel = Math.Max(MapZoom.MinZoomLevel, MapZoom.ZoomLevel / 1.2);

    private void ZoomFit_Click(object sender, RoutedEventArgs e) =>
        MapZoom.FitToCanvas();
}
