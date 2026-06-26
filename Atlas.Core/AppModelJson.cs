using System.Text.Json;
using System.Text.Json.Serialization;

namespace Atlas.Core;

public static class AppModelJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions(indented: true);

    /// <summary>Single-line variant for NDJSON channels — one message per line.</summary>
    public static JsonSerializerOptions Compact { get; } = CreateOptions(indented: false);

    private static JsonSerializerOptions CreateOptions(bool indented)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = indented,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }

    public static AppModel Deserialize(string json) =>
        JsonSerializer.Deserialize<AppModel>(json, Options)
        ?? throw new JsonException("JSON document is not an AppModel.");

    public static string Serialize(AppModel model) =>
        JsonSerializer.Serialize(model, Options);

    /// <summary>Serializes with the compact (single-line) options when <paramref name="compact"/> is set.</summary>
    public static string Serialize(AppModel model, bool compact) =>
        JsonSerializer.Serialize(model, compact ? Compact : Options);
}
