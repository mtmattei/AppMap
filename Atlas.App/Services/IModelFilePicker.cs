namespace Atlas.App.Services;

/// <summary>A picked model file: its filesystem path (when the platform exposes one) and JSON text.</summary>
public sealed record PickedModel(string? Path, string Json);

public interface IModelFilePicker
{
    /// <summary>Lets the user pick an app-model JSON file; null when cancelled.</summary>
    Task<PickedModel?> PickModelAsync(CancellationToken ct);

    /// <summary>Lets the user pick an app's <c>App.xaml.cs</c> to extract from; path only, null when cancelled.</summary>
    Task<string?> PickSourcePathAsync(CancellationToken ct);
}
