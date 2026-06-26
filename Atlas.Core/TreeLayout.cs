namespace Atlas.Core;

/// <summary>
/// Deterministic layered-tree layout for an <see cref="AppModel"/>. Maps the parent→child
/// edge tree to left-to-right columns (by depth), stacks children vertically, and centers
/// each parent against the children placed beneath it — the shape the hand-authored fixtures
/// use. Pure: same model in, same positions out (no force-directed jitter, no clock).
///
/// Only nodes whose <see cref="AppNode.Position"/> is <c>null</c> are placed; any node that
/// already carries a position is preserved, so a stored or hand-authored layout always wins.
/// This is the auto-placement step a freshly extracted model needs before it renders as a
/// graph instead of a pile at the origin.
/// </summary>
public static class TreeLayout
{
    public sealed record Options(
        double OriginX = 80,
        double OriginY = 80,
        double ColumnWidth = 280,
        double RowHeight = 140);

    public static AppModel Apply(AppModel model, Options? options = null)
    {
        var opt = options ?? new Options();

        // Children in edge order; indegree counts every edge so the layout works on a merged
        // (declared + observed) model too, not only a freshly extracted static one.
        var children = model.Nodes.ToDictionary(n => n.Id, _ => new List<string>());
        var indegree = model.Nodes.ToDictionary(n => n.Id, _ => 0);
        foreach (var edge in model.Edges)
        {
            if (children.ContainsKey(edge.From) && children.ContainsKey(edge.To))
            {
                children[edge.From].Add(edge.To);
                indegree[edge.To]++;
            }
        }

        var depth = new Dictionary<string, int>();
        var slot = new Dictionary<string, double>();
        var visited = new HashSet<string>();
        var nextLeaf = 0d;

        // A spanning-tree walk: the first parent to reach a node owns it; back/cross edges in a
        // DAG or cycle are ignored for placement, which keeps the result a clean tidy tree.
        void Place(string id, int d)
        {
            if (!visited.Add(id))
            {
                return;
            }

            depth[id] = d;
            var kids = children[id].Where(c => !visited.Contains(c)).ToList();
            if (kids.Count == 0)
            {
                slot[id] = nextLeaf++;
                return;
            }

            foreach (var kid in kids)
            {
                Place(kid, d + 1);
            }

            var placed = kids.Where(slot.ContainsKey).ToList();
            slot[id] = placed.Count > 0 ? placed.Average(k => slot[k]) : nextLeaf++;
        }

        // Roots first (indegree 0, in declaration order), then anything stranded by a cycle/island.
        foreach (var node in model.Nodes.Where(n => indegree[n.Id] == 0))
        {
            Place(node.Id, 0);
        }
        foreach (var node in model.Nodes)
        {
            Place(node.Id, 0);
        }

        AppNode Position(AppNode node)
        {
            if (node.Position is not null)
            {
                return node;
            }

            var x = opt.OriginX + (depth[node.Id] * opt.ColumnWidth);
            var y = opt.OriginY + (slot[node.Id] * opt.RowHeight);
            return node with { Position = new Point(x, y) };
        }

        return model with { Nodes = model.Nodes.Select(Position).ToList() };
    }

    /// <summary>
    /// Forces a full re-layout into the cleanest navigation tree, ignoring any current positions.
    /// Each node is parented by its highest-priority incoming edge — observed runtime hops first,
    /// then trigger-bearing flow edges, then plain declared (registration) edges — so the lateral
    /// flow becomes the spine instead of the flat shell→child fan-out. Only positions change; the
    /// model's full edge set is preserved.
    /// </summary>
    public static AppModel Untangle(AppModel model, Options? options = null)
    {
        var cleared = model with
        {
            Nodes = model.Nodes.Select(n => n with { Position = null }).ToList(),
            Edges = ParentEdges(model),
        };

        var laidOut = Apply(cleared, options);
        return model with { Nodes = laidOut.Nodes };
    }

    // One incoming edge per node — its highest-priority parent — so layout follows a single clean tree.
    private static IReadOnlyList<AppEdge> ParentEdges(AppModel model)
    {
        static int Score(AppEdge edge) => edge.Kind switch
        {
            EdgeKind.Observed => 3,
            EdgeKind.Unreachable => 0,
            _ => edge.Trigger.Length > 0 ? 2 : 1,   // declared: a flow hop (has a trigger) beats structural
        };

        return model.Edges
            .Select((edge, order) => (edge, order))
            .GroupBy(t => t.edge.To)
            .Select(g => g.OrderByDescending(t => Score(t.edge)).ThenBy(t => t.order).First().edge)
            .ToList();
    }
}
