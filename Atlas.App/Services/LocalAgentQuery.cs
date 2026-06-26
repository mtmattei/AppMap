using Atlas.Core;

namespace Atlas.App.Services;

/// <summary>Dependency-free agent: routes the question through the structural graph queries.</summary>
public sealed class LocalAgentQuery : IAgentQuery
{
    public ValueTask<QueryResult> AnswerAsync(AppModel model, string question, CancellationToken ct) =>
        new(QuestionInterpreter.Answer(model, question));
}
