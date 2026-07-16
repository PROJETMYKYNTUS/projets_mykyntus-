using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Planning.API.Serialization;

/// <summary>
/// Accepte <c>yyyy-MM-dd</c> et les ISO datetime (ex. payload Angular) pour <see cref="DateOnly"/>.
/// </summary>
public sealed class FlexibleDateOnlyJsonConverter : JsonConverter<DateOnly>
{
    public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var s = reader.GetString();
            if (string.IsNullOrWhiteSpace(s))
                throw new JsonException("DateOnly vide.");

            if (DateOnly.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateOnly))
                return dateOnly;

            if (DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto))
                return DateOnly.FromDateTime(dto.Date);

            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
                return DateOnly.FromDateTime(dt);

            throw new JsonException($"Impossible de convertir « {s} » en DateOnly.");
        }

        throw new JsonException($"Token JSON inattendu pour DateOnly : {reader.TokenType}.");
    }

    public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
}

public sealed class FlexibleNullableDateOnlyJsonConverter : JsonConverter<DateOnly?>
{
    private static readonly FlexibleDateOnlyJsonConverter Inner = new();

    public override DateOnly? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;
        return Inner.Read(ref reader, typeof(DateOnly), options);
    }

    public override void Write(Utf8JsonWriter writer, DateOnly? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        Inner.Write(writer, value.Value, options);
    }
}
