using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HackerNewsBestStories.Api.Json;

/// <summary>
/// System.Text.Json's default DateTimeOffset format includes seven digits of fractional
/// seconds, e.g. "2019-10-12T13:43:01.0000000+00:00". The spec in the coding exercise wants
/// plain "2019-10-12T13:43:01+00:00", so this trims it down to whole seconds while keeping
/// it a valid, parseable ISO 8601 timestamp.
/// </summary>
public sealed class RoundedDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    private const string OutputFormat = "yyyy-MM-ddTHH:mm:sszzz";
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return DateTimeOffset.Parse(reader.GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString(OutputFormat, CultureInfo.InvariantCulture));
    }
}