namespace Atlas.Core;

/// <summary>
/// Projects an <see cref="AppModel"/> into the agent panel's suggestion chips. Pure: same model
/// in, same ordered list out. Sits beside <see cref="GraphQueries"/> and reuses its counts so the
/// chips and the answers they dispatch never disagree.
/// </summary>
public static class SuggestionEngine
{
    /// <summary>
    /// Smell chips emit only when their count is &gt; 0 (a clean app shows no noise) and sort by
    /// count descending. Explorer chips always offer: a live trace when a node is on-screen, plus
    /// a path to the deepest reachable leaf. Explorers follow smells; TraceLive leads explorers.
    /// </summary>
    public static IReadOnlyList<Suggestion> For(AppModel model)
    {
        var smells = new List<Suggestion>();

        var orphans = GraphQueries.FindOrphans(model).NodeIds.Count;
        if (orphans > 0)
        {
            smells.Add(new Suggestion("FIND",
                orphans == 1 ? "1 orphaned screen" : $"{orphans} orphaned screens",
                QueryId.Orphans, null, orphans, SuggestionKind.Smell));
        }

        var duplicates = GraphQueries.FindDuplicates(model).NodeIds.Count;
        if (duplicates > 0)
        {
            smells.Add(new Suggestion("DETECT", "Duplicated screens",
                QueryId.Duplicates, null, duplicates, SuggestionKind.Smell));
        }

        var unreachable = model.Edges.Count(e => e.Kind == EdgeKind.Unreachable);
        if (unreachable > 0)
        {
            smells.Add(new Suggestion("FIND",
                unreachable == 1 ? "1 unreachable route" : $"{unreachable} unreachable routes",
                QueryId.Unreachable, null, unreachable, SuggestionKind.Smell));
        }

        // Stable sort by count descending: declaration order breaks ties, so the list is deterministic.
        var ordered = smells
            .Select((s, i) => (s, i))
            .OrderByDescending(t => t.s.Count!.Value)
            .ThenBy(t => t.i)
            .Select(t => t.s)
            .ToList();

        // Explorer: trace the live screen first, when one is connected.
        var live = model.Nodes.FirstOrDefault(n => n.Status == NodeStatus.Live);
        if (live is not null)
        {
            ordered.Add(new Suggestion("TRACE", $"Trace route to {live.Name}",
                QueryId.TraceLive, live.Id, null, SuggestionKind.Explorer));
        }

        // Explorer: every path to the deepest reachable leaf — computed over the graph, never a literal id.
        var target = DeepestLeaf(model);
        if (target is not null)
        {
            ordered.Add(new Suggestion("SHOW", $"Every path to {target.Name}",
                QueryId.PathsTo, target.Id, null, SuggestionKind.Explorer));
        }

        return ordered;
    }

    /// <summary>
    /// The deepest reachable leaf: a non-shell node with no live outbound edge, at maximum BFS
    /// depth from an entry. Reachability ignores unreachable (dead-guard) edges. Ties break by
    /// raw inbound degree (prominence), then by declaration order — keeping selection deterministic.
    /// </summary>
    private static AppNode? DeepestLeaf(AppModel model)
    {
        var liveEdges = model.Edges.Where(e => e.Kind != EdgeKind.Unreachable).ToList();
        var inbound = new HashSet<string>(liveEdges.Select(e => e.To));
        var outbound = liveEdges.ToLookup(e => e.From);
        var entries = model.Nodes.Where(n => !inbound.Contains(n.Id)).Select(n => n.Id);

        var depth = new Dictionary<string, int>();
        var queue = new Queue<string>();
        foreach (var entry in entries)
        {
            depth[entry] = 0;
            queue.Enqueue(entry);
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var edge in outbound[current])
            {
                if (!depth.ContainsKey(edge.To))
                {
                    depth[edge.To] = depth[current] + 1;
                    queue.Enqueue(edge.To);
                }
            }
        }

        var inDegree = model.Nodes.ToDictionary(n => n.Id, n => model.Edges.Count(e => e.To == n.Id));

        AppNode? best = null;
        int bestDepth = -1, bestInbound = -1;
        foreach (var node in model.Nodes)
        {
            if (node.Kind == NodeKind.Shell) continue;        // shells aren't navigation targets
            if (!depth.TryGetValue(node.Id, out var d)) continue;  // orphan / unreachable from any entry
            if (outbound[node.Id].Any()) continue;            // not a leaf

            var inb = inDegree[node.Id];
            if (d > bestDepth || (d == bestDepth && inb > bestInbound))
            {
                best = node;
                bestDepth = d;
                bestInbound = inb;
            }
        }

        return best;
    }
}
