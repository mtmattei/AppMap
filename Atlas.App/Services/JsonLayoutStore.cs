using System.Text.Json;
using Atlas.Core;

namespace Atlas.App.Services;

/// <summary>Stores layouts as JSON under LocalApplicationData/Atlas/layouts/&lt;app&gt;.json.</summary>
public sealed class JsonLayoutStore : ILayoutStore
{
    public IReadOnlyDictionary<string, Point>? Load(string app)
    {
        try
        {
            var path = PathFor(app);
            if (!File.Exists(path))
            {
                return null;
            }

            return JsonSerializer.Deserialize<Dictionary<string, Point>>(File.ReadAllText(path), AppModelJson.Options);
        }
        catch (Exception)
        {
            return null; // a corrupt or unreadable layout never blocks loading the model
        }
    }

    public void Save(string app, IReadOnlyDictionary<string, Point> positions)
    {
        try
        {
            var path = PathFor(app);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(positions, AppModelJson.Options));
        }
        catch (Exception)
        {
            // best effort — losing a layout save is not worth surfacing an error
        }
    }

    private static string PathFor(string app)
    {
        var safe = string.Concat(app.Select(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_' ? c : '_'));
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Atlas", "layouts", $"{safe}.json");
    }
}
