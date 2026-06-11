using Atlas.Core;

namespace Atlas.App.Services;

public interface IAppModelSource
{
    ValueTask<AppModel> LoadAsync(CancellationToken ct);
}
