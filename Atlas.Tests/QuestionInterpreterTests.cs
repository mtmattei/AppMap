using Atlas.Core;

namespace Atlas.Tests;

public class QuestionInterpreterTests
{
    private static AppModel LoadFixture() =>
        AppModelJson.Deserialize(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "fixtures", "rounds-app-model.json")));

    [Theory]
    [InlineData("any orphans?")]
    [InlineData("show ORPHAN screens")]
    public void Orphan_keyword_routes_to_the_orphan_query(string question)
    {
        var model = LoadFixture();

        var answer = QuestionInterpreter.Answer(model, question);

        Assert.Equal(GraphQueries.FindOrphans(model).Answer, answer.Answer);
    }

    [Fact]
    public void Unreachable_keyword_routes_to_the_unreachable_query()
    {
        var model = LoadFixture();

        var answer = QuestionInterpreter.Answer(model, "which routes are unreachable");

        Assert.Equal(GraphQueries.FindUnreachable(model).Answer, answer.Answer);
        Assert.NotEmpty(answer.EdgeKeys); // the fixture has a dead notes→vitals edge
    }

    [Fact]
    public void A_named_screen_traces_every_path_to_it()
    {
        var model = LoadFixture();
        var target = model.Nodes.First(n => n.Kind != NodeKind.Shell);

        var answer = QuestionInterpreter.Answer(model, $"how do I get to {target.Name}");

        Assert.Equal(GraphQueries.FindPathsTo(model, target.Id).Answer, answer.Answer);
    }

    [Fact]
    public void An_unrecognized_question_returns_a_hint_naming_the_app()
    {
        var model = LoadFixture();

        var answer = QuestionInterpreter.Answer(model, "what's the weather like");

        Assert.Empty(answer.NodeIds);
        Assert.Empty(answer.EdgeKeys);
        Assert.Contains(model.App, answer.Answer);
    }

    [Fact]
    public void An_empty_question_returns_a_hint()
    {
        var model = LoadFixture();

        var answer = QuestionInterpreter.Answer(model, "   ");

        Assert.Empty(answer.NodeIds);
        Assert.Contains(model.App, answer.Answer);
    }
}
