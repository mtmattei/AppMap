namespace Atlas.Core;

/// <summary>
/// A bounded context for an agent edit: the focal node plus its immediate flow
/// (direct neighbours) and the files/tokens that edit would touch — not the whole repo.
/// </summary>
public sealed record EditScope(
    AppNode Focus,
    IReadOnlyList<AppNode> Inbound,
    IReadOnlyList<AppNode> Outbound,
    IReadOnlyList<string> Files,
    IReadOnlyList<string> Tokens)
{
    public static EditScope For(AppModel model, string nodeId)
    {
        var focus = model.Nodes.FirstOrDefault(n => n.Id == nodeId)
            ?? throw new ArgumentException($"No node with id '{nodeId}'.", nameof(nodeId));

        var byId = model.Nodes.ToDictionary(n => n.Id);

        var inbound = model.Edges
            .Where(e => e.To == nodeId && byId.ContainsKey(e.From))
            .Select(e => byId[e.From])
            .Distinct()
            .ToList();

        var outbound = model.Edges
            .Where(e => e.From == nodeId && byId.ContainsKey(e.To))
            .Select(e => byId[e.To])
            .Distinct()
            .ToList();

        // Files come from the focus only; the flow is context, not edit surface.
        var files = focus.Files.ToList();
        var tokens = focus.Tokens.ToList();

        return new EditScope(focus, inbound, outbound, files, tokens);
    }

    /// <summary>A compact prompt-ready summary of the scope for an agent hand-off.</summary>
    public string ToPromptContext()
    {
        var lines = new List<string>
        {
            $"Edit scope for {Focus.Name} ({Focus.Kind}, route '{Focus.Route}').",
            $"View: {Focus.View}; ViewModel: {Focus.ViewModel}.",
        };

        if (Files.Count > 0)
        {
            lines.Add("Files: " + string.Join(", ", Files));
        }

        if (Inbound.Count > 0)
        {
            lines.Add("Reached from: " + string.Join(", ", Inbound.Select(n => n.Name)));
        }

        if (Outbound.Count > 0)
        {
            lines.Add("Navigates to: " + string.Join(", ", Outbound.Select(n => n.Name)));
        }

        if (Tokens.Count > 0)
        {
            lines.Add("Theme tokens: " + string.Join(", ", Tokens));
        }

        return string.Join("\n", lines);
    }
}
