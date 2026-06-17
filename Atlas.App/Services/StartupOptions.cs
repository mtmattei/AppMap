namespace Atlas.App.Services;

/// <summary>Startup inputs resolved before the host runs — e.g. a model path from the command line
/// or a file association. Consumed once by <see cref="RuntimeBridge"/> when it loads the first model.</summary>
public sealed record StartupOptions(string? ModelPath);
