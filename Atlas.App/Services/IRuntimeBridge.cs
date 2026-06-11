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
}
