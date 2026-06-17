namespace Atlas.Core;

/// <summary>Ordering and emission policy: smells lead and are count-gated; explorers always offer.</summary>
public enum SuggestionKind { Smell, Explorer }

/// <summary>Which <see cref="GraphQueries"/> call a suggestion dispatches when clicked.</summary>
public enum QueryId { Orphans, Duplicates, Unreachable, PathsTo, TraceLive }

/// <summary>
/// One agent-panel chip, derived from the loaded <see cref="AppModel"/>. The set is a projection
/// of the model, so the panel describes the app in front of it rather than a hardcoded flow.
/// </summary>
public sealed record Suggestion(
    string Verb,          // "FIND" | "SHOW" | "DETECT" | "TRACE" — the colored mono badge
    string Label,         // generated, e.g. "Every path to VitalsEntry"
    QueryId Query,        // dispatch target
    string? Arg,          // target node id for PathsTo/TraceLive; else null
    int? Count,           // pre-counted result size; null = explorer (not pre-run)
    SuggestionKind Kind);
