using Atlas.Core;
using Atlas.Extraction;

namespace Atlas.Tests;

public class TreeLayoutTests
{
    private static readonly DateTimeOffset At = new(2026, 6, 26, 0, 0, 0, TimeSpan.Zero);

    // A two-level tree: Main -> { Dashboard (default), ShiftPlanning }.
    private static AppModel Sample() => RouteExtractor.Extract(
        """
        class App
        {
            void RegisterRoutes(IViewRegistry views, IRouteRegistry routes)
            {
                views.Register(
                    new ViewMap<MainPage, MainModel>(),
                    new ViewMap<DashboardPage, DashboardModel>(),
                    new ViewMap<ShiftPlanningPage, ShiftPlanningModel>());
                routes.Register(
                    new RouteMap("Main", View: views.FindByView<MainPage>(),
                        Nested:
                        [
                            new("Dashboard", View: views.FindByView<DashboardPage>(), IsDefault: true),
                            new("ShiftPlanning", View: views.FindByView<ShiftPlanningPage>())
                        ]));
            }
        }
        """, "Sample", At);

    private static AppNode Node(AppModel m, string route) => m.Nodes.Single(n => n.Route == route);

    [Fact]
    public void Every_null_position_is_placed()
    {
        Assert.All(Sample().Nodes, n => Assert.Null(n.Position)); // precondition: extraction leaves nulls

        var laid = TreeLayout.Apply(Sample());

        Assert.All(laid.Nodes, n => Assert.NotNull(n.Position));
    }

    [Fact]
    public void Depth_drives_columns_siblings_share_one()
    {
        var laid = TreeLayout.Apply(Sample());

        var main = Node(laid, "Main").Position!;
        var dashboard = Node(laid, "Main/Dashboard").Position!;
        var shift = Node(laid, "Main/ShiftPlanning").Position!;

        // Parent sits one column left of its children; the two siblings share the child column.
        Assert.True(main.X < dashboard.X);
        Assert.Equal(dashboard.X, shift.X);
    }

    [Fact]
    public void Parent_is_centered_against_its_children()
    {
        var laid = TreeLayout.Apply(Sample());

        var main = Node(laid, "Main").Position!;
        var dashboard = Node(laid, "Main/Dashboard").Position!;
        var shift = Node(laid, "Main/ShiftPlanning").Position!;

        Assert.Equal((dashboard.Y + shift.Y) / 2, main.Y, precision: 6);
    }

    [Fact]
    public void Existing_positions_are_preserved()
    {
        var model = Sample();
        var pinned = model with
        {
            Nodes = model.Nodes
                .Select(n => n.Route == "Main" ? n with { Position = new Point(999, 999) } : n)
                .ToList(),
        };

        var laid = TreeLayout.Apply(pinned);

        Assert.Equal(new Point(999, 999), Node(laid, "Main").Position);
        Assert.NotNull(Node(laid, "Main/Dashboard").Position); // others still get placed
    }

    [Fact]
    public void Layout_is_deterministic()
    {
        var first = TreeLayout.Apply(Sample());
        var second = TreeLayout.Apply(Sample());

        Assert.Equal(AppModelJson.Serialize(first), AppModelJson.Serialize(second));
    }

    [Fact]
    public void RoundsApp_extracted_then_laid_out_is_a_fanned_tree()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "RoundsApp.App.xaml.cs");
        var model = RouteExtractor.ExtractFromFile(path, "RoundsApp.Mobile", At);

        var laid = TreeLayout.Apply(model);

        Assert.All(laid.Nodes, n => Assert.NotNull(n.Position));

        // Shell is the single left column; all 12 pages share the next column to its right.
        var shell = laid.Nodes.Single(n => n.Kind == NodeKind.Shell).Position!;
        var pages = laid.Nodes.Where(n => n.Kind == NodeKind.Page).Select(n => n.Position!).ToList();

        Assert.All(pages, p => Assert.True(p.X > shell.X));
        Assert.Single(pages.Select(p => p.X).Distinct()); // exactly one page column
        Assert.Equal(12, pages.Select(p => p.Y).Distinct().Count()); // no two pages overlap
    }
}
