#if ATLAS_CLAUDE
using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using Atlas.Core;
using Microsoft.Extensions.Logging;
using AnthropicTextBlock = Anthropic.Models.Messages.TextBlock;

namespace Atlas.App.Services;

/// <summary>
/// Natural-language agent backed by the Claude API (desktop only). The compact AppModel JSON plus
/// the user's question go to the model, which returns a structured answer + the node ids / edge
/// keys to highlight. Any failure — no API key, network error, malformed output — falls back to the
/// local <see cref="QuestionInterpreter"/> so the panel always answers.
/// </summary>
public sealed class ClaudeAgentQuery(ILogger<ClaudeAgentQuery> logger) : IAgentQuery
{
    private const string Model = "claude-opus-4-8";

    private const string SystemPrompt =
        "You are Atlas's agent. You read an app's navigation graph (the AppModel JSON) and answer " +
        "questions about its structure: screens (nodes), routes, and the edges between them. " +
        "Answer in 1-3 sentences. Highlight the relevant parts by returning the node ids and edge " +
        "keys involved. An edge key is \"<from>><to>\" built from node ids (e.g. \"shell>login\"). " +
        "Only reference ids that exist in the model; return empty arrays when nothing should highlight.";

    // The model fills this shape (mirrors QueryResult). additionalProperties:false keeps it exact.
    private static readonly Dictionary<string, JsonElement> AnswerSchema = new()
    {
        ["type"] = JsonSerializer.SerializeToElement("object"),
        ["properties"] = JsonSerializer.SerializeToElement(new
        {
            answer = new { type = "string" },
            nodeIds = new { type = "array", items = new { type = "string" } },
            edgeKeys = new { type = "array", items = new { type = "string" } },
        }),
        ["required"] = JsonSerializer.SerializeToElement(new[] { "answer", "nodeIds", "edgeKeys" }),
        ["additionalProperties"] = JsonSerializer.SerializeToElement(false),
    };

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async ValueTask<QueryResult> AnswerAsync(AppModel model, string question, CancellationToken ct)
    {
        try
        {
            var client = new AnthropicClient();   // reads ANTHROPIC_API_KEY from the environment
            var response = await client.Messages.Create(new MessageCreateParams
            {
                Model = Model,
                MaxTokens = 1024,
                System = SystemPrompt,
                Messages =
                [
                    new()
                    {
                        Role = Role.User,
                        Content = $"Model:\n{AppModelJson.Serialize(model, compact: true)}\n\nQuestion: {question}",
                    },
                ],
                OutputConfig = new OutputConfig { Format = new JsonOutputFormat { Schema = AnswerSchema } },
            });

            var json = response.Content.Select(b => b.Value).OfType<AnthropicTextBlock>().FirstOrDefault()?.Text;
            if (json is { Length: > 0 } && JsonSerializer.Deserialize<Shape>(json, ReadOptions) is { } shape)
            {
                return new QueryResult(shape.NodeIds ?? [], shape.EdgeKeys ?? [], shape.Answer ?? "");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Claude agent query failed; falling back to the local interpreter.");
        }

        return QuestionInterpreter.Answer(model, question);
    }

    private sealed record Shape(string? Answer, IReadOnlyList<string>? NodeIds, IReadOnlyList<string>? EdgeKeys);
}
#endif
