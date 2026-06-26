namespace Atlas.App.Services;

/// <summary>Startup inputs resolved before the host runs — a model JSON path, or an app's
/// <c>App.xaml.cs</c> to extract from (e.g. when the agent auto-launches the viewer). Consumed once
/// by <see cref="RuntimeBridge"/> when it loads the first model.</summary>
public sealed record StartupOptions(string? ModelPath, string? SourcePath = null);
