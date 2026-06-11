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
}
