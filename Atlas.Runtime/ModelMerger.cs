using Atlas.Core;

namespace Atlas.Runtime;

/// <summary>
/// Merges observed runtime routes into an AppModel. Pure: same model + event in, same model out.
/// Provenance is preserved — a declared edge that fires becomes observed, never the reverse.
/// </summary>
public static class ModelMerger
{
    /// <summary>
    /// Resolves a runtime route path to a node. Tries, in order: route template match
    /// (segment-wise, "{x}" matches anything), route suffix match, then node Name/View
    /// match on the last segment — notifier path formats vary with region nesting.
    /// </summary>
    public static AppNode? ResolveNode(AppModel model, string routePath)
    {
        if (string.IsNullOrWhiteSpace(routePath))
        {
            return null;
        }

        var segments = Normalize(routePath);

        foreach (var node in model.Nodes)
        {
            if (TemplateMatches(Normalize(node.Route), segments))
            {
                return node;
            }
        }

        // Suffix fallback compares literal segments only — a trailing "{id}" template
        // would otherwise match any unknown route.
        var last = segments[segments.Length - 1];
        foreach (var node in model.Nodes)
        {
            var nodeSegments = Normalize(node.Route);
            if (nodeSegments.Length > 0
                && !IsParameter(nodeSegments[nodeSegments.Length - 1])
                && string.Equals(nodeSegments[nodeSegments.Length - 1], last, StringComparison.OrdinalIgnoreCase))
            {
                return node;
            }
        }

        foreach (var node in model.Nodes)
        {
            if (string.Equals(node.Name, last, StringComparison.OrdinalIgnoreCase)
                || string.Equals(node.View, last, StringComparison.OrdinalIgnoreCase)
                || string.Equals(node.View, last + "Page", StringComparison.OrdinalIgnoreCase))
            {
                return node;
            }
        }

        return null;
    }

    /// <summary>
    /// Applies one observed navigation: the target node goes Live (the previous Live
    /// reverts to Normal), and the from→to edge is marked Observed — flipping a
    /// declared/unreachable edge that actually fired, or adding a new observed edge.
    /// </summary>
    public static AppModel ApplyRoute(AppModel model, string? fromNodeId, string toNodeId, DateTimeOffset timestamp)
    {
        var nodes = model.Nodes
            .Select(n =>
                n.Id == toNodeId
                    ? n with { Status = NodeStatus.Live }
                    : n.Status == NodeStatus.Live
                        ? n with { Status = NodeStatus.Normal }
                        : n)
            .ToList();

        var edges = model.Edges.ToList();
        if (fromNodeId is not null && fromNodeId != toNodeId)
        {
            var index = edges.FindIndex(e => e.From == fromNodeId && e.To == toNodeId);
            if (index >= 0)
            {
                edges[index] = edges[index] with { Kind = EdgeKind.Observed };
            }
            else if (!edges.Any(e => e.From == toNodeId && e.To == fromNodeId))
            {
                // Reverse of an existing edge = back navigation: move the live node,
                // but don't invent a forward edge for it.
                edges.Add(new AppEdge(fromNodeId, toNodeId, EdgeKind.Observed, Trigger: string.Empty));
            }
        }

        return model with
        {
            Nodes = nodes,
            Edges = edges,
            Source = ModelSource.Merged,
            GeneratedAt = timestamp,
        };
    }

    private static string[] Normalize(string route) =>
        route.Trim().TrimStart('.').Trim('/').Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

    private static bool TemplateMatches(string[] template, string[] actual)
    {
        if (template.Length == 0 || template.Length != actual.Length)
        {
            return false;
        }

        for (var i = 0; i < template.Length; i++)
        {
            if (!SegmentMatches(template[i], actual[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool SegmentMatches(string template, string actual) =>
        IsParameter(template) || string.Equals(template, actual, StringComparison.OrdinalIgnoreCase);

    private static bool IsParameter(string segment) =>
        segment.StartsWith("{", StringComparison.Ordinal) && segment.EndsWith("}", StringComparison.Ordinal);
}
