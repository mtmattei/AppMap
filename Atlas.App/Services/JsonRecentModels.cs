using System.Text.Json;
using Atlas.Core;

namespace Atlas.App.Services;

/// <summary>
/// Stores recent model paths as JSON under LocalApplicationData/Atlas/recent.json, mirroring
/// <see cref="JsonLayoutStore"/>. MRU ordering and capping is the pure <see cref="RecentList"/>.
/// The root is injectable so the list can be exercised against a temp directory in tests.
/// </summary>
public sealed class JsonRecentModels : IRecentModels
{
    private readonly string _root;

    public JsonRecentModels(string? root = null) =>
        _root = root ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Atlas");

    public IReadOnlyList<string> All()
    {
        try
        {
            var path = PathFor();
            if (!File.Exists(path))
            {
                return Array.Empty<string>();
            }

            return JsonSerializer.Deserialize<List<string>>(File.ReadAllText(path), AppModelJson.Options)
                ?? (IReadOnlyList<string>)Array.Empty<string>();
        }
        catch (Exception)
        {
            return Array.Empty<string>(); // a corrupt recents file never blocks the app
        }
    }

    public void Add(string path)
    {
        try
        {
            var promoted = RecentList.Promote(All(), path);
            var file = PathFor();
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, JsonSerializer.Serialize(promoted, AppModelJson.Options));
        }
        catch (Exception)
        {
            // best effort — losing a recents write is not worth surfacing an error
        }
    }

    private string PathFor() => Path.Combine(_root, "recent.json");
}
