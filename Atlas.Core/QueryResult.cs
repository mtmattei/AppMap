namespace Atlas.Core;

/// <summary>
/// Result of a structural graph query: the node ids and edge keys ("from>to") to
/// highlight on the canvas, plus the prose answer the agent panel shows.
/// </summary>
public sealed record QueryResult(
    IReadOnlyList<string> NodeIds,
    IReadOnlyList<string> EdgeKeys,
    string Answer);
