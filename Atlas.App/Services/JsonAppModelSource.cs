using Atlas.Core;

namespace Atlas.App.Services;

/// <summary>Loads the embedded sample app model. Replaced by extraction output in Phase 3.</summary>
public sealed class JsonAppModelSource : IAppModelSource
{
    private const string ResourceName = "Atlas.App.Fixtures.rounds-app-model.json";

    public async ValueTask<AppModel> LoadAsync(CancellationToken ct)
    {
        using var stream = typeof(JsonAppModelSource).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new FileNotFoundException($"Embedded app model '{ResourceName}' not found.");
        using var reader = new StreamReader(stream);
        var json = await reader.ReadToEndAsync(ct);
        return AppModelJson.Deserialize(json);
    }
}
