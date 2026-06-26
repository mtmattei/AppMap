using Atlas.Core;
using Atlas.Extraction;

namespace Atlas.Tests;

public class RouteExtractorTests
{
    private static readonly DateTimeOffset At = new(2026, 6, 26, 0, 0, 0, TimeSpan.Zero);

    private static AppModel LoadFixture(string name) =>
        AppModelJson.Deserialize(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", name)));

    // ---- shape coverage (synthetic snippets) --------------------------------

    [Fact]
    public void Root_with_viewmodel_only_becomes_a_shell_node()
    {
        const string source = """
            class App
            {
                void RegisterRoutes(IViewRegistry views, IRouteRegistry routes)
                {
                    views.Register(
                        new ViewMap(ViewModel: typeof(ShellModel)),
                        new ViewMap<HomePage>());
                    routes.Register(
                        new RouteMap("", View: views.FindByViewModel<ShellModel>(),
                            Nested:
                            [
                                new("Home", View: views.FindByView<HomePage>(), IsDefault: true)
                            ]));
                }
            }
            """;

        var model = RouteExtractor.Extract(source, "Sample", At);

        var shell = Assert.Single(model.Nodes, n => n.Kind == NodeKind.Shell);
        Assert.Equal("", shell.Route);
        Assert.Equal("ShellModel", shell.ViewModel);
        Assert.Equal("", shell.View); // VM-only registration has no view

        var home = Assert.Single(model.Nodes, n => n.Route == "Home");
        Assert.Equal(NodeKind.Page, home.Kind);
        Assert.Equal("HomePage", home.View);
        Assert.Equal("HomeModel", home.ViewModel); // XxxPage -> XxxModel convention
    }

    [Fact]
    public void Nested_routes_compose_full_paths_and_carry_declared_edges()
    {
        const string source = """
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
            """;

        var model = RouteExtractor.Extract(source, "Sample", At);

        Assert.Equal(
            new[] { "Main", "Main/Dashboard", "Main/ShiftPlanning" },
            model.Nodes.Select(n => n.Route));

        Assert.All(model.Edges, e => Assert.Equal(EdgeKind.Declared, e.Kind));
        var defaultEdge = Assert.Single(model.Edges, e => e.IsDefault);
        var dashboard = Assert.Single(model.Nodes, n => n.Route == "Main/Dashboard");
        Assert.Equal(dashboard.Id, defaultEdge.To);
    }

    [Fact]
    public void Explicit_and_data_viewmaps_win_over_convention()
    {
        const string source = """
            class App
            {
                void RegisterRoutes(IViewRegistry views, IRouteRegistry routes)
                {
                    views.Register(
                        new ViewMap(ViewModel: typeof(ShellModel)),
                        new ViewMap<ListPage, ListVm>(),
                        new DataViewMap<DetailPage, DetailVm, Item>());
                    routes.Register(
                        new RouteMap("", View: views.FindByViewModel<ShellModel>(),
                            Nested:
                            [
                                new("List", View: views.FindByView<ListPage>()),
                                new("Detail", View: views.FindByView<DetailPage>())
                            ]));
                }
            }
            """;

        var model = RouteExtractor.Extract(source, "Sample", At);

        Assert.Equal("ListVm", Assert.Single(model.Nodes, n => n.Route == "List").ViewModel);
        Assert.Equal("DetailVm", Assert.Single(model.Nodes, n => n.Route == "Detail").ViewModel);
    }

    [Fact]
    public void Bare_nested_entry_without_a_view_still_produces_a_node()
    {
        const string source = """
            class App
            {
                void RegisterRoutes(IViewRegistry views, IRouteRegistry routes)
                {
                    views.Register(new ViewMap(ViewModel: typeof(MainModel)));
                    routes.Register(
                        new RouteMap("Main", View: views.FindByViewModel<MainModel>(),
                            Nested:
                            [
                                new("ForYouTab", IsDefault: true),
                                new("FavoritesTab")
                            ]));
                }
            }
            """;

        var model = RouteExtractor.Extract(source, "Sample", At);

        var forYou = Assert.Single(model.Nodes, n => n.Route == "Main/ForYouTab");
        Assert.Equal("ForYouTab", forYou.Name);
        Assert.Equal("", forYou.View);     // no View: reference to resolve
        Assert.Equal(2, model.Edges.Count);
    }

    [Fact]
    public void DependsOn_adds_a_declared_edge_from_dependency_to_dependent()
    {
        // From the Uno docs: Products DependsOn Main — Main is navigated before Products.
        const string source = """
            class App
            {
                void RegisterRoutes(IViewRegistry views, IRouteRegistry routes)
                {
                    views.Register(
                        new ViewMap(ViewModel: typeof(ShellModel)),
                        new ViewMap<MainPage, MainModel>(),
                        new ViewMap<ProductsPage, ProductsModel>());
                    routes.Register(
                        new RouteMap("", View: views.FindByViewModel<ShellModel>(),
                            Nested:
                            [
                                new("Main", View: views.FindByView<MainPage>(), IsDefault: true),
                                new("Products", View: views.FindByView<ProductsPage>(), DependsOn: "Main")
                            ]));
                }
            }
            """;

        var model = RouteExtractor.Extract(source, "Sample", At);

        var main = Assert.Single(model.Nodes, n => n.Route == "Main");
        var products = Assert.Single(model.Nodes, n => n.Route == "Products");

        // The structural shell→child edges still stand; the dependency adds one more.
        var dep = Assert.Single(model.Edges, e => e.DependsOn);
        Assert.Equal(main.Id, dep.From);
        Assert.Equal(products.Id, dep.To);
        Assert.Equal(EdgeKind.Declared, dep.Kind);

        // No structural edge is marked DependsOn; the shell still fans out to both children.
        Assert.Equal(3, model.Edges.Count);
        Assert.Equal(2, model.Edges.Count(e => e.From != main.Id && !e.DependsOn));
    }

    [Fact]
    public void DependsOn_naming_an_unknown_route_is_dropped()
    {
        const string source = """
            class App
            {
                void RegisterRoutes(IViewRegistry views, IRouteRegistry routes)
                {
                    views.Register(new ViewMap(ViewModel: typeof(MainModel)));
                    routes.Register(
                        new RouteMap("Main", View: views.FindByViewModel<MainModel>(),
                            Nested:
                            [
                                new("Products", DependsOn: "Ghost")
                            ]));
                }
            }
            """;

        var model = RouteExtractor.Extract(source, "Sample", At);

        // Only the structural Main→Products edge survives; the unresolved DependsOn is dropped.
        var edge = Assert.Single(model.Edges);
        Assert.False(edge.DependsOn);
    }

    // ---- golden tie: reconstructed RegisterRoutes reproduces a hand-authored fixture ----

    [Fact]
    public void Nursing_registration_reproduces_the_fixture_declared_structure()
    {
        // The RegisterRoutes that the hand-authored nursing fixture was modeled from.
        const string source = """
            class App
            {
                void RegisterRoutes(IViewRegistry views, IRouteRegistry routes)
                {
                    views.Register(
                        new ViewMap<MainPage, MainModel>(),
                        new ViewMap<DashboardPage, DashboardModel>(),
                        new ViewMap<ShiftPlanningPage, ShiftPlanningModel>(),
                        new ViewMap<StaffDirectoryPage, StaffDirectoryModel>(),
                        new ViewMap<RequestsPage, RequestsModel>(),
                        new ViewMap<WorkloadPage, WorkloadModel>());
                    routes.Register(
                        new RouteMap("Main", View: views.FindByView<MainPage>(),
                            Nested:
                            [
                                new("Dashboard", View: views.FindByView<DashboardPage>(), IsDefault: true),
                                new("ShiftPlanning", View: views.FindByView<ShiftPlanningPage>()),
                                new("StaffDirectory", View: views.FindByView<StaffDirectoryPage>()),
                                new("Requests", View: views.FindByView<RequestsPage>()),
                                new("Workload", View: views.FindByView<WorkloadPage>())
                            ]));
                }
            }
            """;
        var fixture = LoadFixture("nursing-app-model.json");

        var model = RouteExtractor.Extract(source, fixture.App, At);

        Assert.Equal(ModelSource.Static, model.Source);
        Assert.Equal(fixture.Nodes.Count, model.Nodes.Count);

        // Match by route (the static-derivable key); ids/positions/triggers are out of static scope.
        var byRoute = model.Nodes.ToDictionary(n => n.Route);
        foreach (var expected in fixture.Nodes)
        {
            var actual = Assert.Contains(expected.Route, byRoute);
            Assert.Equal(expected.View, actual.View);
            Assert.Equal(expected.ViewModel, actual.ViewModel);
            Assert.Equal(expected.Kind, actual.Kind);
        }

        // Same declared edge fan-out, same single default edge target.
        Assert.Equal(fixture.Edges.Count, model.Edges.Count);
        Assert.All(model.Edges, e => Assert.Equal(EdgeKind.Declared, e.Kind));

        var expectedDefaultRoute = fixture.Nodes.Single(n =>
            n.Id == fixture.Edges.Single(e => e.IsDefault).To).Route;
        var actualDefaultRoute = model.Nodes.Single(n =>
            n.Id == model.Edges.Single(e => e.IsDefault).To).Route;
        Assert.Equal(expectedDefaultRoute, actualDefaultRoute);
    }

    // ---- real-world integration: the actual RoundsApp source ----------------

    [Fact]
    public void Real_RoundsApp_source_yields_the_declared_route_graph()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "RoundsApp.App.xaml.cs");

        var model = RouteExtractor.ExtractFromFile(path, "RoundsApp.Mobile", At);

        Assert.Equal("RoundsApp.Mobile", model.App);
        Assert.Equal(ModelSource.Static, model.Source);

        // Root shell (ShellModel) + 12 nested page routes.
        Assert.Equal(13, model.Nodes.Count);
        var shell = Assert.Single(model.Nodes, n => n.Kind == NodeKind.Shell);
        Assert.Equal("", shell.Route);
        Assert.Equal("ShellModel", shell.ViewModel);

        // Every declared edge fans out from the shell; exactly one is the default (Login).
        Assert.Equal(12, model.Edges.Count);
        Assert.All(model.Edges, e => Assert.Equal(shell.Id, e.From));
        Assert.All(model.Edges, e => Assert.Equal(EdgeKind.Declared, e.Kind));

        var defaultEdge = Assert.Single(model.Edges, e => e.IsDefault);
        Assert.Equal("Login", model.Nodes.Single(n => n.Id == defaultEdge.To).Route);

        // Convention-resolved view-model on a view-only ViewMap<LoginPage>().
        var detail = Assert.Single(model.Nodes, n => n.Route == "PatientDetail");
        Assert.Equal("PatientDetailPage", detail.View);
        Assert.Equal("PatientDetailModel", detail.ViewModel);
    }

    [Fact]
    public void Extract_then_layout_positions_every_node_and_round_trips()
    {
        // The exact pipeline the atlas CLI runs: extract → tree layout → serialize.
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "RoundsApp.App.xaml.cs");

        var model = TreeLayout.Apply(RouteExtractor.ExtractFromFile(path, "RoundsApp", At));

        Assert.All(model.Nodes, n => Assert.NotNull(n.Position));

        var round = AppModelJson.Deserialize(AppModelJson.Serialize(model));
        Assert.Equal(model.Nodes.Count, round.Nodes.Count);
        Assert.Equal(model.Edges.Count, round.Edges.Count);
    }

    [Fact]
    public void Extraction_is_deterministic()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "RoundsApp.App.xaml.cs");

        var first = RouteExtractor.ExtractFromFile(path, "RoundsApp.Mobile", At);
        var second = RouteExtractor.ExtractFromFile(path, "RoundsApp.Mobile", At);

        Assert.Equal(AppModelJson.Serialize(first), AppModelJson.Serialize(second));
    }
}
