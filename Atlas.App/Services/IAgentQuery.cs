using Atlas.Core;

namespace Atlas.App.Services;

/// <summary>
/// Answers a free-text agent question against the loaded <see cref="AppModel"/>, returning the
/// answer text plus the node ids / edge keys to highlight on the canvas. The local implementation
/// routes to the structural <see cref="GraphQueries"/>; the Claude implementation (desktop only)
/// asks the model natural-language and falls back to local on any failure.
/// </summary>
public interface IAgentQuery
{
    ValueTask<QueryResult> AnswerAsync(AppModel model, string question, CancellationToken ct);
}
