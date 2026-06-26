namespace Atlas.Core;

/// <summary>
/// Structural queries over an AppModel. Pure functions: same model in, same answer out.
/// The agent panel and any external agent run these against the same document the canvas renders.
/// </summary>
public static class GraphQueries
{
    public static string EdgeKey(AppEdge edge) => $"{edge.From}>{edge.To}";

    /// <summary>
    /// Orphans: nodes with a registered route but no inbound and no outbound edges.
    /// Entry screens (no inbound, with outbound) are not orphans — the app starts there.
    /// </summary>
    public static QueryResult FindOrphans(AppModel model)
    {
        var inbound = new HashSet<string>(model.Edges.Select(e => e.To));
        var outbound = new HashSet<string>(model.Edges.Select(e => e.From));

        var orphans = model.Nodes
            .Where(n => n.Kind != NodeKind.Shell && !inbound.Contains(n.Id) && !outbound.Contains(n.Id))
            .ToList();

        var answer = orphans.Count == 0
            ? "No orphans. Every registered route has at least one connection in the graph."
            : orphans.Count == 1
                ? $"One orphan. {orphans[0].Name} has a registered route ({orphans[0].Route}) but no inbound navigation anywhere in the graph. It is reachable by deep link only — likely a missing entry point, or dead code from a removed flow."
                : $"{orphans.Count} orphans with registered routes and no navigation in or out: {string.Join(", ", orphans.Select(n => n.Name))}.";

        return new QueryResult(orphans.Select(n => n.Id).ToList(), Array.Empty<string>(), answer);
    }

    /// <summary>All simple paths from entry nodes (no inbound edges) to the target node.</summary>
    public static QueryResult FindPathsTo(AppModel model, string targetId)
    {
        var target = model.Nodes.FirstOrDefault(n => n.Id == targetId);
        if (target is null)
        {
            return new QueryResult(Array.Empty<string>(), Array.Empty<string>(), $"No node with id '{targetId}' in the model.");
        }

        var inbound = new HashSet<string>(model.Edges.Select(e => e.To));
        var entries = model.Nodes.Where(n => !inbound.Contains(n.Id) && n.Id != targetId).Select(n => n.Id);
        var edgesByFrom = model.Edges.ToLookup(e => e.From);

        var paths = new List<IReadOnlyList<AppEdge>>();
        foreach (var entry in entries)
        {
            Walk(entry, new List<AppEdge>(), new HashSet<string> { entry });
        }

        void Walk(string current, List<AppEdge> path, HashSet<string> visited)
        {
            if (current == targetId)
            {
                paths.Add(path.ToList());
                return;
            }

            foreach (var edge in edgesByFrom[current])
            {
                if (visited.Add(edge.To))
                {
                    path.Add(edge);
                    Walk(edge.To, path, visited);
                    path.RemoveAt(path.Count - 1);
                    visited.Remove(edge.To);
                }
            }
        }

        var nodesById = model.Nodes.ToDictionary(n => n.Id);
        var nodeIds = paths.SelectMany(p => p.SelectMany(e => new[] { e.From, e.To })).Distinct().ToList();
        var edgeKeys = paths.SelectMany(p => p.Select(EdgeKey)).Distinct().ToList();

        string Describe(IReadOnlyList<AppEdge> path) =>
            string.Join(" → ", path.Select(e => nodesById[e.From].Name).Append(target.Name));

        var answer = paths.Count == 0
            ? $"No path reaches {target.Name} from any entry screen. It is reachable by deep link only."
            : paths.Count == 1
                ? $"A single path: {Describe(paths[0])}. The final hop is {DescribeKind(paths[0][paths[0].Count - 1])}."
                // One path per line so each is distinguishable in the narrow agent column.
                : $"{paths.Count} paths reach {target.Name}:\n" +
                  string.Join("\n", paths.Select((p, i) => $"{i + 1}. {Describe(p)}"));

        return new QueryResult(nodeIds, edgeKeys, answer);

        static string DescribeKind(AppEdge edge) => edge.Kind switch
        {
            EdgeKind.Declared => "declared but never observed at runtime — no coverage on this hop",
            EdgeKind.Unreachable => "registered but unreachable — its guard never fires",
            _ => "observed at runtime",
        };
    }

    /// <summary>
    /// Duplicates: non-shell nodes that declare at least one identical element.
    /// Same element on two screens is the smell that two views do the same job.
    /// </summary>
    public static QueryResult FindDuplicates(AppModel model)
    {
        var pairs = new List<(AppNode A, AppNode B, IReadOnlyList<string> Shared)>();
        var candidates = model.Nodes.Where(n => n.Kind != NodeKind.Shell).ToList();
        for (var i = 0; i < candidates.Count; i++)
        {
            for (var j = i + 1; j < candidates.Count; j++)
            {
                var shared = candidates[i].Elements.Intersect(candidates[j].Elements).ToList();
                if (shared.Count > 0)
                {
                    pairs.Add((candidates[i], candidates[j], shared));
                }
            }
        }

        var nodeIds = pairs.SelectMany(p => new[] { p.A.Id, p.B.Id }).Distinct().ToList();
        var answer = pairs.Count == 0
            ? "No duplicated screens. No two views declare the same element."
            : string.Join(" ", pairs.Select(p =>
                $"{p.A.Name} and {p.B.Name} both declare {string.Join(", ", p.Shared)} through different view-models. Same job, divergent UI — consolidate or they will drift."));

        return new QueryResult(nodeIds, Array.Empty<string>(), answer);
    }

    /// <summary>Unreachable: declared edges whose guard never fires (provenance Unreachable).</summary>
    public static QueryResult FindUnreachable(AppModel model)
    {
        var dead = model.Edges.Where(e => e.Kind == EdgeKind.Unreachable).ToList();
        var nodesById = model.Nodes.ToDictionary(n => n.Id);

        var nodeIds = dead.SelectMany(e => new[] { e.From, e.To }).Distinct().ToList();
        var edgeKeys = dead.Select(EdgeKey).ToList();
        var answer = dead.Count == 0
            ? "No unreachable routes. Every declared edge can fire."
            : string.Join(" ", dead.Select(e =>
                $"{nodesById[e.From].Name} → {nodesById[e.To].Name} is registered but its navigation guard can never evaluate true. Dead edge. Remove the route or fix the guard ({e.Trigger})."));

        return new QueryResult(nodeIds, edgeKeys, answer);
    }
}
