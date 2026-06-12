using Atlas.Core;

namespace Atlas.App.Services;

/// <summary>Persists hand-arranged node positions per app, keyed by node id.</summary>
public interface ILayoutStore
{
    IReadOnlyDictionary<string, Point>? Load(string app);

    void Save(string app, IReadOnlyDictionary<string, Point> positions);
}
