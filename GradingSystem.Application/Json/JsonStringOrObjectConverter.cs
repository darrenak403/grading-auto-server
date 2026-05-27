using System.Text.Json;
using System.Text.Json.Serialization;

namespace GradingSystem.Application.Json;

/// <summary>
/// Bidirectional converter for inputJson / expectJson fields:
///
///   Read  — accepts both a JSON string token and a raw JSON object/array:
///             "inputJson": "{\"key\":\"val\"}"  → stored as string
///             "inputJson": {"key":"val"}         → captured as string via GetRawText()
///             "inputJson": null                  → null
///
///   Write — emits stored string as a raw JSON value (object/array/string),
///           so API responses return clean JSON instead of an escaped string.
/// </summary>
public sealed class JsonStringOrObjectConverter : JsonConverter<string?>
{
    public override string? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;

            case JsonTokenType.String:
                return reader.GetString();

            case JsonTokenType.StartObject:
            case JsonTokenType.StartArray:
            {
                using var doc = JsonDocument.ParseValue(ref reader);
                return doc.RootElement.GetRawText();
            }

            default:
                throw new JsonException(
                    $"Unexpected token '{reader.TokenType}' for inputJson/expectJson. " +
                    "Expected a JSON string, object, array, or null.");
        }
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(value);
            doc.RootElement.WriteTo(writer);
        }
        catch (JsonException)
        {
            writer.WriteStringValue(value);
        }
    }
}
