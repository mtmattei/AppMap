using Atlas.Core;

namespace Atlas.Tests;

public class GraphQueryTests
{
    private static AppModel Model { get; } = AppModelJson.Deserialize(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "rounds-app-model.json")));

    [Fact]
    public void Orphans_finds_handoff_only()
    {
        var result = GraphQueries.FindOrphans(Model);

        Assert.Equal(new[] { "handoff" }, result.NodeIds);
        Assert.Empty(result.EdgeKeys);
        Assert.Contains("HandoffReport", result.Answer);
    }

    [Fact]
    public void Paths_to_meds_is_the_single_main_flow()
    {
        var result = GraphQueries.FindPathsTo(Model, "meds");

        Assert.Equal(
            new[] { "login", "shell", "dash", "plist", "pdetail", "meds" }.OrderBy(x => x),
            result.NodeIds.OrderBy(x => x));
        Assert.Equal(
            new[] { "login>shell", "shell>dash", "dash>plist", "plist>pdetail", "pdetail>meds" }.OrderBy(x => x),
            result.EdgeKeys.OrderBy(x => x));
        Assert.Contains("single path", result.Answer);
        Assert.Contains("declared but never observed", result.Answer);
    }

    [Fact]
    public void Paths_to_unknown_node_reports_missing()
    {
        var result = GraphQueries.FindPathsTo(Model, "nope");

        Assert.Empty(result.NodeIds);
        Assert.Contains("nope", result.Answer);
    }

    [Fact]
    public void Duplicates_finds_the_two_vitals_screens()
    {
        var result = GraphQueries.FindDuplicates(Model);

        Assert.Equal(
            new[] { "qvitals", "vitals" }.OrderBy(x => x),
            result.NodeIds.OrderBy(x => x));
        Assert.Contains("NumberBox HR", result.Answer);
    }

    [Fact]
    public void Unreachable_finds_the_dead_notes_edge()
    {
        var result = GraphQueries.FindUnreachable(Model);

        Assert.Equal(new[] { "notes>vitals" }, result.EdgeKeys);
        Assert.Equal(
            new[] { "notes", "vitals" }.OrderBy(x => x),
            result.NodeIds.OrderBy(x => x));
        Assert.Contains("guard", result.Answer);
    }
}
