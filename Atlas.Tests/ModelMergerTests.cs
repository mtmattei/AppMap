using Atlas.Core;
using Atlas.Runtime;

namespace Atlas.Tests;

public class ModelMergerTests
{
    private static AppModel Model => AppModelJson.Deserialize(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "rounds-app-model.json")));

    [Fact]
    public void Resolves_exact_route()
    {
        Assert.Equal("dash", ModelMerger.ResolveNode(Model, "dashboard")?.Id);
    }

    [Fact]
    public void Resolves_route_template_with_parameter()
    {
        Assert.Equal("pdetail", ModelMerger.ResolveNode(Model, "patients/123")?.Id);
        Assert.Equal("vitals", ModelMerger.ResolveNode(Model, "patients/123/vitals")?.Id);
    }

    [Fact]
    public void Resolves_by_name_when_route_does_not_match()
    {
        Assert.Equal("dash", ModelMerger.ResolveNode(Model, "shell/Dashboard")?.Id);
        Assert.Equal("plist", ModelMerger.ResolveNode(Model, "PatientListPage")?.Id);
    }

    [Fact]
    public void Unknown_route_resolves_to_null()
    {
        Assert.Null(ModelMerger.ResolveNode(Model, "nonexistent/route"));
    }

    [Fact]
    public void ApplyRoute_moves_live_status()
    {
        var when = DateTimeOffset.UtcNow;
        var merged = ModelMerger.ApplyRoute(Model, "pdetail", "vitals", when);

        Assert.Equal(NodeStatus.Live, merged.Nodes.Single(n => n.Id == "vitals").Status);
        Assert.Equal(NodeStatus.Normal, merged.Nodes.Single(n => n.Id == "pdetail").Status);
        Assert.Equal(ModelSource.Merged, merged.Source);
        Assert.Equal(when, merged.GeneratedAt);
    }

    [Fact]
    public void ApplyRoute_flips_declared_edge_to_observed()
    {
        var merged = ModelMerger.ApplyRoute(Model, "pdetail", "meds", DateTimeOffset.UtcNow);

        var edge = merged.Edges.Single(e => e.From == "pdetail" && e.To == "meds");
        Assert.Equal(EdgeKind.Observed, edge.Kind);
        Assert.Equal("AdministerMeds", edge.Trigger); // trigger text survives the flip
    }

    [Fact]
    public void ApplyRoute_adds_new_observed_edge_when_none_declared()
    {
        var merged = ModelMerger.ApplyRoute(Model, "dash", "handoff", DateTimeOffset.UtcNow);

        var edge = merged.Edges.Single(e => e.From == "dash" && e.To == "handoff");
        Assert.Equal(EdgeKind.Observed, edge.Kind);
        Assert.Equal(Model.Edges.Count + 1, merged.Edges.Count);
    }

    [Fact]
    public void ApplyRoute_without_previous_node_only_moves_live()
    {
        var merged = ModelMerger.ApplyRoute(Model, null, "login", DateTimeOffset.UtcNow);

        Assert.Equal(NodeStatus.Live, merged.Nodes.Single(n => n.Id == "login").Status);
        Assert.Equal(Model.Edges.Count, merged.Edges.Count);
    }
}
