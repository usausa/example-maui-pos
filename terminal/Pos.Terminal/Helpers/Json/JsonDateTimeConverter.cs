namespace Pos.Terminal.Helpers.Json;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

// 日時は UTC (書式は DateTimeHelper.ToIsoDateTime)
public sealed class JsonDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (String.IsNullOrEmpty(value))
        {
            return default;
        }

        try
        {
            return DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
        }
        catch (FormatException ex)
        {
            throw new JsonException($"Invalid datetime format. value=[{value}]", ex);
        }
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        var utc = value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : value.ToUniversalTime();
        writer.WriteStringValue(DateTimeHelper.ToIsoDateTime(utc));
    }
}
