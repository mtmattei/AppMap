namespace Atlas.App.Services;

/// <summary>A picked model file: its filesystem path (when the platform exposes one) and JSON text.</summary>
public sealed record PickedModel(string? Path, string Json);

public interface IModelFilePicker
{
    /// <summary>Lets the user pick an app-model JSON file; null when cancelled.</summary>
    Task<PickedModel?> PickModelAsync(CancellationToken ct);
}
