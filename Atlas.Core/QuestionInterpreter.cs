namespace Atlas.Core;

/// <summary>
/// Turns a free-text agent question into a <see cref="QueryResult"/> by routing it to the existing
/// structural <see cref="GraphQueries"/>. Pure: same model + question in, same answer out. This is
/// the local, dependency-free interpreter behind the agent panel's "ask your own question" box —
/// a keyword/screen-name router, not a language model. Wiring the Claude API for true natural
/// language is the Phase-4 upgrade that would replace the body of <see cref="Answer"/>.
/// </summary>
public static class QuestionInterpreter
{
    public static QueryResult Answer(AppModel model, string question)
    {
        var text = question.Trim();
        if (text.Length == 0)
        {
            return Hint(model);
        }

        var lower = text.ToLowerInvariant();

        if (Mentions(lower, "orphan")) return GraphQueries.FindOrphans(model);
        if (Mentions(lower, "duplicate", "dupe", "redundant")) return GraphQueries.FindDuplicates(model);
        if (Mentions(lower, "unreachable", "dead", "guard")) return GraphQueries.FindUnreachable(model);

        // Otherwise the question is about a screen: trace every path that reaches it.
        if (ResolveNode(model, text, lower) is { } node)
        {
            return GraphQueries.FindPathsTo(model, node.Id);
        }

        return Hint(model);
    }

    private static bool Mentions(string lower, params string[] keywords) =>
        keywords.Any(k => lower.Contains(k, StringComparison.Ordinal));

    // Match the screen the question names: exact name/route/id first, then the longest node name
    // that appears anywhere in the text, so "show me the patient detail page" still resolves.
    private static AppNode? ResolveNode(AppModel model, string text, string lower)
    {
        var exact = model.Nodes.FirstOrDefault(n =>
            n.Name.Equals(text, StringComparison.OrdinalIgnoreCase) ||
            n.Route.Equals(text, StringComparison.OrdinalIgnoreCase) ||
            n.Id.Equals(text, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact;
        }

        return model.Nodes
            .Where(n => n.Name.Length > 0 && lower.Contains(n.Name.ToLowerInvariant(), StringComparison.Ordinal))
            .OrderByDescending(n => n.Name.Length)
            .FirstOrDefault();
    }

    // No keyword and no named screen: tell the user what this local agent can answer.
    private static QueryResult Hint(AppModel model)
    {
        var example = model.Nodes.FirstOrDefault(n => n.Kind != NodeKind.Shell)?.Name ?? "a screen";
        var answer =
            $"I can answer structural questions about {model.App}. Name a screen to trace every path to it " +
            $"(e.g. \"paths to {example}\"), or ask about orphans, duplicates, or unreachable routes.";
        return new QueryResult(Array.Empty<string>(), Array.Empty<string>(), answer);
    }
}
