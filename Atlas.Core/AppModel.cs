namespace Atlas.Core;

public sealed record AppModel(
    string App,
    DateTimeOffset GeneratedAt,
    ModelSource Source,
    string SchemaVersion,
    IReadOnlyList<AppNode> Nodes,
    IReadOnlyList<AppEdge> Edges);

public sealed record AppNode(
    string Id,
    string Name,
    NodeKind Kind,
    string Route,
    string View,
    string ViewModel,
    NodeStatus Status,
    IReadOnlyList<string> Files,
    IReadOnlyList<string> Elements,
    IReadOnlyList<string> Tokens,
    Point? Position);

public sealed record AppEdge(
    string From,
    string To,
    EdgeKind Kind,
    string Trigger,
    bool IsDefault = false,
    bool DependsOn = false);

public sealed record Point(double X, double Y);

public enum ModelSource { Static, Runtime, Merged }

public enum NodeKind { Shell, Page, Dialog }

public enum NodeStatus { Normal, Live, Orphan }

public enum EdgeKind { Declared, Observed, Unreachable }
