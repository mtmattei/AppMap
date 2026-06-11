using Atlas.Core;

namespace Atlas.Tests;

public class AppModelRoundTripTests
{
    private static string FixturePath =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", "rounds-app-model.json");

    private static AppModel LoadFixture() =>
        AppModelJson.Deserialize(File.ReadAllText(FixturePath));

    [Fact]
    public void Fixture_deserializes_with_expected_shape()
    {
        var model = LoadFixture();

        Assert.Equal("RoundsApp.Mobile", model.App);
        Assert.Equal(ModelSource.Merged, model.Source);
        Assert.Equal("1.0", model.SchemaVersion);
        Assert.Equal(new DateTimeOffset(2026, 6, 11, 0, 0, 0, TimeSpan.Zero), model.GeneratedAt);
        Assert.Equal(12, model.Nodes.Count);
        Assert.Equal(11, model.Edges.Count);
    }

    [Fact]
    public void Fixture_nodes_map_kinds_statuses_and_positions()
    {
        var model = LoadFixture();

        var shell = Assert.Single(model.Nodes, n => n.Id == "shell");
        Assert.Equal(NodeKind.Shell, shell.Kind);
        Assert.Equal("", shell.Route);

        var qvitals = Assert.Single(model.Nodes, n => n.Id == "qvitals");
        Assert.Equal(NodeKind.Dialog, qvitals.Kind);

        var pdetail = Assert.Single(model.Nodes, n => n.Id == "pdetail");
        Assert.Equal(NodeStatus.Live, pdetail.Status);
        Assert.Equal("patients/{id}", pdetail.Route);
        Assert.Equal("PatientDetailPage", pdetail.View);
        Assert.Equal("PatientDetailModel", pdetail.ViewModel);
        Assert.Equal(new Point(1130, 104), pdetail.Position);
        Assert.Equal(
            new[] { "Views/PatientDetailPage.xaml", "Presentation/PatientDetailModel.cs" },
            pdetail.Files);

        var handoff = Assert.Single(model.Nodes, n => n.Id == "handoff");
        Assert.Equal(NodeStatus.Orphan, handoff.Status);
    }

    [Fact]
    public void Fixture_edges_map_kinds_and_flags()
    {
        var model = LoadFixture();

        var login = Assert.Single(model.Edges, e => e.From == "login");
        Assert.Equal(EdgeKind.Observed, login.Kind);
        Assert.Equal("OnAuthenticated", login.Trigger);

        var shellDash = Assert.Single(model.Edges, e => e.From == "shell" && e.To == "dash");
        Assert.True(shellDash.IsDefault);

        var rounding = Assert.Single(model.Edges, e => e.To == "rounding");
        Assert.Equal(EdgeKind.Declared, rounding.Kind);
        Assert.True(rounding.DependsOn);

        var dead = Assert.Single(model.Edges, e => e.Kind == EdgeKind.Unreachable);
        Assert.Equal("notes", dead.From);
        Assert.Equal("vitals", dead.To);
    }

    [Fact]
    public void Fixture_round_trips_without_loss()
    {
        var first = LoadFixture();

        var json = AppModelJson.Serialize(first);
        var second = AppModelJson.Deserialize(json);

        // Records hold lists, so compare canonical serialized forms instead of references.
        Assert.Equal(json, AppModelJson.Serialize(second));
        Assert.Equal(first.Nodes.Count, second.Nodes.Count);
        Assert.Equal(first.Edges.Count, second.Edges.Count);
    }
}
