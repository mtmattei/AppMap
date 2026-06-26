using Atlas.Core;
using Atlas.Extraction;

namespace Atlas.Tests;

public class TriggerExtractorTests
{
    private static readonly DateTimeOffset At = new(2026, 6, 26, 0, 0, 0, TimeSpan.Zero);

    // A small two-page app: a shell fanning out to PatientDetail and RoundingChecklist.
    private static AppModel TwoPageApp()
    {
        const string routes = """
            class App
            {
                void RegisterRoutes(IViewRegistry views, IRouteRegistry routes)
                {
                    views.Register(
                        new ViewMap(ViewModel: typeof(ShellModel)),
                        new ViewMap<PatientDetailPage, PatientDetailModel>(),
                        new ViewMap<RoundingChecklistPage, RoundingChecklistModel>());
                    routes.Register(
                        new RouteMap("", View: views.FindByViewModel<ShellModel>(),
                            Nested:
                            [
                                new("PatientDetail", View: views.FindByView<PatientDetailPage>(), IsDefault: true),
                                new("RoundingChecklist", View: views.FindByView<RoundingChecklistPage>())
                            ]));
                }
            }
            """;
        return RouteExtractor.Extract(routes, "Sample", At);
    }

    // ---- XAML pass -----------------------------------------------------------

    [Fact]
    public void Xaml_request_adds_a_labelled_flow_edge_between_pages()
    {
        const string xaml = """
            <Page x:Class="Sample.Presentation.PatientDetailPage"
                  xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                  xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                  xmlns:uen="using:Uno.Extensions.Navigation.UI">
              <Button Content="Start rounding" uen:Navigation.Request="RoundingChecklist" />
              <Button Content="Back" uen:Navigation.Request="-" />
            </Page>
            """;

        var model = TriggerExtractor.AddXamlTriggers(TwoPageApp(), [xaml]);

        var pdetail = Assert.Single(model.Nodes, n => n.Route == "PatientDetail");
        var rounding = Assert.Single(model.Nodes, n => n.Route == "RoundingChecklist");

        var flow = Assert.Single(model.Edges, e => e.From == pdetail.Id && e.To == rounding.Id);
        Assert.Equal("Start rounding", flow.Trigger);
        Assert.Equal(EdgeKind.Declared, flow.Kind);

        // The "-" back request makes no forward edge: only the 2 structural + 1 flow edge exist.
        Assert.Equal(3, model.Edges.Count);
    }

    [Fact]
    public void Xaml_prefers_x_name_over_content_for_the_trigger_label()
    {
        const string xaml = """
            <Page x:Class="Sample.PatientDetailPage"
                  xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                  xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                  xmlns:uen="using:Uno.Extensions.Navigation.UI">
              <Button x:Name="StartRoundingButton" Content="Start rounding"
                      uen:Navigation.Request="RoundingChecklist" />
            </Page>
            """;

        var model = TriggerExtractor.AddXamlTriggers(TwoPageApp(), [xaml]);

        var flow = Assert.Single(model.Edges, e => e.Trigger.Length > 0);
        Assert.Equal("StartRoundingButton", flow.Trigger);
    }

    [Fact]
    public void Data_bound_request_resolves_to_no_edge()
    {
        const string xaml = """
            <Page x:Class="Sample.PatientDetailPage"
                  xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                  xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                  xmlns:uen="using:Uno.Extensions.Navigation.UI">
              <Button Content="Go" uen:Navigation.Request="{Binding Target}" />
            </Page>
            """;

        var model = TriggerExtractor.AddXamlTriggers(TwoPageApp(), [xaml]);

        // Only the 2 structural edges survive; the dynamic target is unresolved and dropped.
        Assert.Equal(2, model.Edges.Count);
        Assert.All(model.Edges, e => Assert.Equal("", e.Trigger));
    }

    [Fact]
    public void Xaml_qualifier_prefixed_request_resolves_the_route_after_the_qualifier()
    {
        const string xaml = """
            <Page x:Class="Sample.PatientDetailPage"
                  xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                  xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                  xmlns:uen="using:Uno.Extensions.Navigation.UI">
              <Button Content="Deep link" uen:Navigation.Request="-/RoundingChecklist" />
              <Button Content="Parent" uen:Navigation.Request="../RoundingChecklist" />
              <Button Content="Pure back" uen:Navigation.Request="-" />
            </Page>
            """;

        var model = TriggerExtractor.AddXamlTriggers(TwoPageApp(), [xaml]);

        var pdetail = Assert.Single(model.Nodes, n => n.Route == "PatientDetail");
        var rounding = Assert.Single(model.Nodes, n => n.Route == "RoundingChecklist");

        // Both qualifier+route buttons resolve to the same hop (deduped); the bare "-" makes none.
        var flow = Assert.Single(model.Edges, e => e.From == pdetail.Id && e.To == rounding.Id);
        Assert.Equal("Deep link", flow.Trigger);
        Assert.Equal(3, model.Edges.Count); // 2 structural + 1 flow
    }

    // ---- code pass -----------------------------------------------------------

    [Fact]
    public void Code_navigate_route_uses_the_enclosing_method_as_trigger()
    {
        const string code = """
            public partial record PatientDetailModel(INavigator Navigator)
            {
                public async Task StartRounding() =>
                    await Navigator.NavigateRouteAsync(this, "RoundingChecklist");
            }
            """;

        var model = TriggerExtractor.AddCodeTriggers(TwoPageApp(), [code]);

        var pdetail = Assert.Single(model.Nodes, n => n.Route == "PatientDetail");
        var rounding = Assert.Single(model.Nodes, n => n.Route == "RoundingChecklist");

        var flow = Assert.Single(model.Edges, e => e.From == pdetail.Id && e.To == rounding.Id);
        Assert.Equal("StartRounding", flow.Trigger);
    }

    [Fact]
    public void Code_navigate_viewmodel_resolves_target_by_type_argument()
    {
        const string code = """
            public partial record PatientDetailModel(INavigator Navigator)
            {
                public Task OpenChecklist() => Navigator.NavigateViewModelAsync<RoundingChecklistModel>(this);
            }
            """;

        var model = TriggerExtractor.AddCodeTriggers(TwoPageApp(), [code]);

        var pdetail = Assert.Single(model.Nodes, n => n.Route == "PatientDetail");
        var rounding = Assert.Single(model.Nodes, n => n.Route == "RoundingChecklist");

        var flow = Assert.Single(model.Edges, e => e.From == pdetail.Id && e.To == rounding.Id);
        Assert.Equal("OpenChecklist", flow.Trigger);
    }

    [Fact]
    public void Code_navigate_data_resolves_target_through_the_dataviewmap()
    {
        // NavigateDataAsync(this, new T()) resolves T via a DataViewMap<View, VM, T> registration.
        const string code = """
            public partial record PatientDetailModel(INavigator Navigator)
            {
                void Register(IViewRegistry views) =>
                    views.Register(new DataViewMap<RoundingChecklistPage, RoundingChecklistModel, ChecklistItem>());

                public Task StartFromItem() => Navigator.NavigateDataAsync(this, new ChecklistItem());
            }
            """;

        var model = TriggerExtractor.AddCodeTriggers(TwoPageApp(), [code]);

        var pdetail = Assert.Single(model.Nodes, n => n.Route == "PatientDetail");
        var rounding = Assert.Single(model.Nodes, n => n.Route == "RoundingChecklist");

        var flow = Assert.Single(model.Edges, e => e.From == pdetail.Id && e.To == rounding.Id);
        Assert.Equal("StartFromItem", flow.Trigger);
    }

    [Fact]
    public void Code_back_navigation_makes_no_forward_edge()
    {
        const string code = """
            public partial record RoundingChecklistModel(INavigator Navigator)
            {
                public Task Done() => Navigator.NavigateBackAsync(this);
            }
            """;

        var model = TriggerExtractor.AddCodeTriggers(TwoPageApp(), [code]);

        Assert.Equal(2, model.Edges.Count); // only the structural fan-out
    }

    // ---- real-world integration: the actual RoundsApp sources ----------------

    [Fact]
    public void Real_RoundsApp_xaml_recovers_the_patient_detail_flow()
    {
        var appPath = Path.Combine(AppContext.BaseDirectory, "fixtures", "RoundsApp.App.xaml.cs");
        var xamlPath = Path.Combine(AppContext.BaseDirectory, "fixtures", "PatientDetailPage.xaml");

        var routes = RouteExtractor.ExtractFromFile(appPath, "RoundsApp.Mobile", At);
        var model = TriggerExtractor.AddXamlTriggers(routes, [File.ReadAllText(xamlPath)]);

        var pdetail = Assert.Single(model.Nodes, n => n.Route == "PatientDetail");

        // The four forward buttons become labelled flow edges; the "Back" button makes none.
        var flow = model.Edges
            .Where(e => e.From == pdetail.Id && e.Trigger.Length > 0)
            .ToDictionary(e => model.Nodes.Single(n => n.Id == e.To).Route, e => e.Trigger);

        Assert.Equal("Start rounding", flow["RoundingChecklist"]);
        Assert.Equal("Record vitals", flow["VitalsEntry"]);
        Assert.Equal("Administer meds", flow["MedAdministration"]);
        Assert.Equal("Add note", flow["ClinicalNotes"]);
        Assert.Equal(4, flow.Count);
    }

    // ---- in-place labelling --------------------------------------------------

    [Fact]
    public void A_trigger_labels_an_existing_structural_hop_in_place()
    {
        // The shell already declares shell→PatientDetail structurally (no trigger). A code call from
        // the shell to that same route should label the existing edge, not add a duplicate.
        const string code = """
            public partial record ShellModel(INavigator Navigator)
            {
                public Task Enter() => Navigator.NavigateRouteAsync(this, "PatientDetail");
            }
            """;

        var before = TwoPageApp();
        var model = TriggerExtractor.AddCodeTriggers(before, [code]);

        Assert.Equal(before.Edges.Count, model.Edges.Count); // no new edge
        var shell = Assert.Single(model.Nodes, n => n.Kind == NodeKind.Shell);
        var pdetail = Assert.Single(model.Nodes, n => n.Route == "PatientDetail");
        var hop = Assert.Single(model.Edges, e => e.From == shell.Id && e.To == pdetail.Id);
        Assert.Equal("Enter", hop.Trigger);
        Assert.True(hop.IsDefault); // the structural flag is preserved through the relabel
    }
}
