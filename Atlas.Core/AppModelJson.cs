using System.Text.Json;
using System.Text.Json.Serialization;

namespace Atlas.Core;

public static class AppModelJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }

    public static AppModel Deserialize(string json) =>
        JsonSerializer.Deserialize<AppModel>(json, Options)
        ?? throw new JsonException("JSON document is not an AppModel.");

    public static string Serialize(AppModel model) =>
        JsonSerializer.Serialize(model, Options);
}
