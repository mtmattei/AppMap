namespace Atlas.App.Services;

public interface IModelFilePicker
{
    /// <summary>Lets the user pick an app-model JSON file; null when cancelled.</summary>
    Task<string?> PickModelJsonAsync(CancellationToken ct);
}
