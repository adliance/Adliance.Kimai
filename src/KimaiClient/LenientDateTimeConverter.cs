using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Adliance.Kimai.KimaiClient;

public sealed class LenientDateTimeConverter : JsonConverter<DateTime>
{
    private static readonly string[] AllowedFormats =
    {
        "yyyy-MM-dd",
        "yyyy-MM-dd'T'HH:mm:ssK",
        "yyyy-MM-dd'T'HH:mm:sszzz",
        "yyyy-MM-dd'T'HH:mm:sszzzz" // für +0100
    };

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => DateTime.ParseExact(reader.GetString()!, AllowedFormats, CultureInfo.InvariantCulture, DateTimeStyles.None);

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture));

    public static LenientDateTimeConverter Instance { get; } = new();
}

public sealed class LenientDateTimeNullableConverter : JsonConverter<DateTime?>
{
    private static readonly string[] AllowedFormats =
    {
        "yyyy-MM-dd",
        "yyyy-MM-dd'T'HH:mm:ssK",
        "yyyy-MM-dd'T'HH:mm:sszzz",
        "yyyy-MM-dd'T'HH:mm:sszzzz" // für +0100
    };

    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var s = reader.GetString();
        if (string.IsNullOrWhiteSpace(s)) return null;
        return DateTime.ParseExact(s, AllowedFormats, CultureInfo.InvariantCulture, DateTimeStyles.None);
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
        => writer.WriteStringValue(value?.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture));

    public static LenientDateTimeNullableConverter Instance { get; } = new();
}
