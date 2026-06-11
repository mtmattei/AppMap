namespace Atlas.Core;

/// <summary>
/// One NDJSON line on the Atlas agent channel. The first message a client sends
/// has a null Route and identifies the app; subsequent messages report route changes.
/// </summary>
public sealed record AgentMessage(
    string App,
    string? Route,
    DateTimeOffset Timestamp);
