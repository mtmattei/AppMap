namespace Atlas.Core;

/// <summary>Constants shared by both ends of the Atlas agent channel.</summary>
public static class AtlasChannel
{
    public const int DefaultPort = 9743;
}

/// <summary>
/// One NDJSON line on the Atlas agent channel. The first message a client sends
/// has a null Route and identifies the app; subsequent messages report route changes.
/// </summary>
public sealed record AgentMessage(
    string App,
    string? Route,
    DateTimeOffset Timestamp);

/// <summary>
/// A command from the viewer back to a connected agent (one NDJSON line each way).
/// v1 carries navigation requests for "Jump to (live)".
/// </summary>
public sealed record AgentCommand(
    string Kind,
    string Route)
{
    public const string Navigate = "navigate";
}
