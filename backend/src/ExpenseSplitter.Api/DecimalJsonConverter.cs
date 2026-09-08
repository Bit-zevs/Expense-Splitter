using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ExpenseSplitter.Api;

// Monetary JSON values are strings so browser clients preserve every cent.
public sealed class DecimalJsonConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetDecimal(out var number))
            return number;
        if (reader.TokenType == JsonTokenType.String
            && decimal.TryParse(reader.GetString(), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var value))
            return value;
        throw new JsonException("Invalid decimal.");
    }

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
}
