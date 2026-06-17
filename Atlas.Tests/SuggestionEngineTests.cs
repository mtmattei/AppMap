using Atlas.Core;

namespace Atlas.Tests;

public class SuggestionEngineTests
{
    private static AppModel Load(string fixture) => AppModelJson.Deserialize(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", fixture)));

    private static AppModel Rounds { get; } = Load("rounds-app-model.json");
    private static AppModel Nursing { get; } = Load("nursing-app-model.json");

    [Fact]
    public void Rounds_surfaces_smells_first_sorted_by_count()
    {
        var suggestions = SuggestionEngine.For(Rounds);

        var smells = suggestions.Where(s => s.Kind == SuggestionKind.Smell).ToList();

        // Duplicates (qvitals + vitals = 2 nodes) outranks the single orphan and single dead edge.
        Assert.Equal(
            new[] { QueryId.Duplicates, QueryId.Orphans, QueryId.Unreachable },
            smells.Select(s => s.Query));
        Assert.Equal(new int?[] { 2, 1, 1 }, smells.Select(s => s.Count));

        // Smells lead the list; explorers follow.
        Assert.True(
            suggestions.TakeWhile(s => s.Kind == SuggestionKind.Smell).Count() == smells.Count,
            "All smell chips must precede every explorer chip.");
    }

    [Fact]
    public void Rounds_pathsTo_target_is_a_deep_leaf_not_a_literal()
    {
        var pathsTo = SuggestionEngine.For(Rounds).Single(s => s.Query == QueryId.PathsTo);

        // Deepest reachable leaf, tie-broken by inbound degree → VitalsEntry (two inbound edges).
        Assert.Equal("vitals", pathsTo.Arg);
        Assert.Contains("VitalsEntry", pathsTo.Label);

        // The query resolves to a real node, never the old hardcoded "meds".
        Assert.NotNull(GraphQueries.FindPathsTo(Rounds, pathsTo.Arg!).NodeIds);
    }

    [Fact]
    public void Rounds_traces_the_live_screen_first_among_explorers()
    {
        var explorers = SuggestionEngine.For(Rounds)
            .Where(s => s.Kind == SuggestionKind.Explorer)
            .ToList();

        // pdetail is Live in the fixture → TraceLive leads explorers, dispatching PathsTo on its id.
        Assert.Equal(QueryId.TraceLive, explorers[0].Query);
        Assert.Equal("pdetail", explorers[0].Arg);
        Assert.Contains("PatientDetail", explorers[0].Label);
    }

    [Fact]
    public void Nursing_is_clean_so_no_smell_chips()
    {
        var suggestions = SuggestionEngine.For(Nursing);

        Assert.DoesNotContain(suggestions, s => s.Kind == SuggestionKind.Smell);
    }

    [Fact]
    public void Nursing_offers_one_explorer_to_a_real_leaf()
    {
        var suggestions = SuggestionEngine.For(Nursing);

        // No Live node on a static model → no TraceLive, just the deepest-leaf path.
        var explorer = Assert.Single(suggestions);
        Assert.Equal(QueryId.PathsTo, explorer.Query);

        var leaves = new[] { "dashboard", "shiftplanning", "staffdirectory", "requests", "workload" };
        Assert.Contains(explorer.Arg, leaves);
    }

    [Fact]
    public void Nursing_never_mentions_the_old_meds_literal()
    {
        var suggestions = SuggestionEngine.For(Nursing);

        Assert.DoesNotContain(suggestions, s => s.Arg == "meds");
        Assert.DoesNotContain(suggestions, s => s.Label.Contains("meds", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Same_model_in_yields_identical_ordered_list_out()
    {
        var first = SuggestionEngine.For(Rounds);
        var second = SuggestionEngine.For(Rounds);

        Assert.Equal(first, second); // record value-equality across the whole ordered sequence
    }
}
