using Atlas.Core;

namespace Atlas.App.Services;

public interface IRuntimeBridge
{
    /// <summary>The current app model followed by every runtime-merged update.</summary>
    IAsyncEnumerable<AppModel> Models(CancellationToken ct);

    /// <summary>The current agent-connection state followed by every change.</summary>
    IAsyncEnumerable<bool> Connection(CancellationToken ct);
}
