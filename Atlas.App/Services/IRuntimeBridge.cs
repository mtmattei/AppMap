using Atlas.Core;

namespace Atlas.App.Services;

public interface IRuntimeBridge
{
    /// <summary>The current app model followed by every runtime-merged update.</summary>
    IAsyncEnumerable<AppModel> Models(CancellationToken ct);

    /// <summary>The current agent-connection state followed by every change.</summary>
    IAsyncEnumerable<bool> Connection(CancellationToken ct);

    /// <summary>Moves a node to a new canvas position and re-emits the model.</summary>
    void MoveNode(string nodeId, double x, double y);

    /// <summary>Replaces every node position (e.g. an auto-layout), persists it, and re-emits.</summary>
    void ApplyLayout(IReadOnlyDictionary<string, Atlas.Core.Point> positions);

    /// <summary>Replaces the current model (e.g. a model file opened by the user).</summary>
    void OpenModel(AppModel model);

    /// <summary>True while an agent is connected and can receive jump commands.</summary>
    bool IsConnected { get; }

    /// <summary>Asks the connected agent to navigate to a route (best effort).</summary>
    void RequestNavigate(string route);
}
