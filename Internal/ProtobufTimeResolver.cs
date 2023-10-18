using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;

using System.Reflection;
using System.Globalization;

using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;

using JsonConverter = Newtonsoft.Json.JsonConverter;

using Type = System.Type;

namespace Protoc.Gateway.Internal;

internal class ProtobufTimeResolver : DefaultContractResolver
{
    public ProtobufTimeResolver() => NamingStrategy = new CamelCaseNamingStrategy();

    protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
    {
        JsonProperty property = base.CreateProperty(member, memberSerialization);

        if (property.PropertyType == typeof(Timestamp))
        {
            property.Converter = new TimeStampConverter();
        }

        Type? propertyType = property.PropertyType;
        Type? type = propertyType?.GenericTypeArguments.FirstOrDefault();

        if (type is not null && property.PropertyType == typeof(MapField<,>).MakeGenericType(type, typeof(Timestamp)))
        {
            property.Converter = (JsonConverter?)Activator.CreateInstance(typeof(MapTimeConverter<>).MakeGenericType(type));
        }

        if (property.PropertyType == typeof(Duration))
        {
            property.Converter = new DurationConverter();
        }

        return property;
    }

    public class TimeStampConverter : DateTimeConverterBase
    {
        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            Timestamp timestamp = value as Timestamp ?? Timestamp.FromDateTime(DateTime.MinValue);

            writer.WriteValue(timestamp.ToString()[1..^1]);
        }

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            string source = reader.Value?.ToString() ?? DateTime.MinValue.ToString(CultureInfo.InvariantCulture);

            DateTime dateTimeValue = DateTime.Parse(source, CultureInfo.InvariantCulture);

            return Timestamp.FromDateTime(dateTimeValue.ToUniversalTime());
        }
    }

    public class DurationConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            Duration duration = (Duration?)value ?? Duration.FromTimeSpan(TimeSpan.MinValue);
            writer.WriteValue(duration.ToString());
        }

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            return Duration.FromTimeSpan(
                TimeSpan.Parse(reader.Value?.ToString() ?? TimeSpan.MinValue.ToString(),
                CultureInfo.InvariantCulture));
        }

        public override bool CanConvert(Type objectType) => objectType == typeof(Duration) || objectType == typeof(TimeSpan);
    }

    public class MapTimeConverter<TKey> : JsonConverter where TKey : notnull
    {
        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            MapField<TKey, Timestamp> mapFields = (MapField<TKey, Timestamp>?)value ?? new MapField<TKey, Timestamp>();

            Dictionary<TKey, DateTime> dictionary = mapFields.ToDictionary(
                k => k.Key,
                v => v.Value.ToDateTime());

            serializer.Serialize(writer, dictionary);
        }

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer) => null;

        public override bool CanConvert(Type objectType) => objectType == typeof(MapField<TKey, Timestamp>) || objectType == typeof(TimeSpan);
    }
}
