namespace Atlas.App.Presentation;

/// <summary>Command parameter for repositioning a node on the canvas.</summary>
public sealed record NodeMove(string NodeId, double X, double Y);
