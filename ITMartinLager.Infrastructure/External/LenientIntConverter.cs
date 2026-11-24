using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ITMartinLager.Infrastructure.External;

// The AI sometimes answers a number as text ("1973", "ca. 1975", "1970'erne") or as 1973.0 (user 2026-11-24: "AI'en kunne ikke svare:
// The JSON value could not be converted to ... Path: $.year"). Read what can be read; anything else = empty, never a failed answer.
public sealed partial class LenientIntConverter : JsonConverter<int?>
{
    public override int? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null: return null;
            case JsonTokenType.Number:
                if (reader.TryGetInt32(out var i)) return i;
                return reader.TryGetDouble(out var d) && d is > int.MinValue and < int.MaxValue ? (int)Math.Round(d) : null;
            case JsonTokenType.String:
                var s = reader.GetString() ?? "";
                if (int.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)) return n;
                return FirstNumber().Match(s) is { Success: true } m && int.TryParse(m.Value, out var f) ? f : null;
            case JsonTokenType.True: case JsonTokenType.False: return null;
            default:
                reader.Skip();
                return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, int? value, JsonSerializerOptions options)
    {
        if (value is { } v) writer.WriteNumberValue(v); else writer.WriteNullValue();
    }

    [GeneratedRegex(@"\d+")]
    private static partial Regex FirstNumber();
}
