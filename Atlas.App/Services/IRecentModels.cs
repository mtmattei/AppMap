namespace Atlas.App.Services;

/// <summary>Remembers recently-loaded model file paths (MRU), so the user can reopen without browsing.</summary>
public interface IRecentModels
{
    /// <summary>The remembered paths, newest first.</summary>
    IReadOnlyList<string> All();

    /// <summary>Records a load: promotes the path to the front, dedupes, caps the list.</summary>
    void Add(string path);
}
