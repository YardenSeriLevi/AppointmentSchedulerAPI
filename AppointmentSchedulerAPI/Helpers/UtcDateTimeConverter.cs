using System.Text.Json;
using System.Text.Json.Serialization;

public class UtcDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var dateTime = reader.GetDateTime();

        // If the incoming date is specified as local, convert it to UTC.
        // If it's Unspecified, assume it's local (common case from JavaScript).
        if (dateTime.Kind == DateTimeKind.Local || dateTime.Kind == DateTimeKind.Unspecified)
        {
            return dateTime.ToUniversalTime();
        }

        // If it's already UTC, just return it.
        return dateTime;
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToUniversalTime().ToString("o"));
    }
}