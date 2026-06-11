namespace Atlas.App.Presentation;

public sealed partial class MapPage : Page
{
    public MapPage()
    {
        this.InitializeComponent();
        Loaded += (_, _) =>
        {
            // Docked right panel: drawer sits opposite its open direction.
            PanelDrawer.OpenDirection = Uno.Toolkit.UI.DrawerOpenDirection.Left;
            PanelDrawer.DrawerDepth = 372;
            PanelDrawer.FitToDrawerContent = false;
            PanelDrawer.IsLightDismissEnabled = false;
            PanelDrawer.IsOpen = true;
        };
    }

    private void ZoomIn_Click(object sender, RoutedEventArgs e) =>
        MapZoom.ZoomLevel = Math.Min(MapZoom.MaxZoomLevel, MapZoom.ZoomLevel * 1.2);

    private void ZoomOut_Click(object sender, RoutedEventArgs e) =>
        MapZoom.ZoomLevel = Math.Max(MapZoom.MinZoomLevel, MapZoom.ZoomLevel / 1.2);

    private void ZoomFit_Click(object sender, RoutedEventArgs e) =>
        MapZoom.FitToCanvas();
}
