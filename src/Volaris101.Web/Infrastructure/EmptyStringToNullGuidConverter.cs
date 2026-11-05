using System.Text.Json;
using System.Text.Json.Serialization;

namespace Volaris101.Web.Infrastructure;

public sealed class EmptyStringToNullGuidConverter : JsonConverter<Guid?>
{
    public override Guid? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Null => null,
            JsonTokenType.String when string.IsNullOrWhiteSpace(reader.GetString()) => null,
            JsonTokenType.String when Guid.TryParse(reader.GetString(), out var value) => value,
            JsonTokenType.String => throw new JsonException("Invalid GUID value."),
            _ => reader.GetGuid()
        };
    }

    public override void Write(Utf8JsonWriter writer, Guid? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
            writer.WriteStringValue(value.Value);
        else
            writer.WriteNullValue();
    }
}
