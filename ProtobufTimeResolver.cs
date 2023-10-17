using Google.Protobuf.WellKnownTypes;

using System.Reflection;

using Type = System.Type;
using Google.Protobuf.Collections;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;
using System.Globalization;
using JsonConverter = Newtonsoft.Json.JsonConverter;

namespace Protoc.Gateway;

public class ProtobufTimeResolver : DefaultContractResolver
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
            Timestamp? timestamp = value as Timestamp;
            timestamp ??= Timestamp.FromDateTime(DateTime.MinValue);

            string str1 = timestamp.ToString();
            string str2 = str1[1..^1];

            writer.WriteValue(str2);
        }

        public override object? ReadJson(
          JsonReader reader,
          Type objectType,
          object? existingValue,
          JsonSerializer serializer)
        {
            return Timestamp.FromDateTime(DateTime.Parse(reader.Value?.ToString() ?? DateTime.MinValue.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture).ToUniversalTime());
        }
    }

    public class DurationConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            Duration duration = (Duration?)value ?? Duration.FromTimeSpan(TimeSpan.MinValue);
            writer.WriteValue(duration.ToString());
        }

        public override object? ReadJson(
          JsonReader reader,
          Type objectType,
          object? existingValue,
          JsonSerializer serializer)
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
            Dictionary<TKey, DateTime> dictionary = ((MapField<TKey, Timestamp>?)value ?? new MapField<TKey, Timestamp>()).ToDictionary(k => k.Key, v => v.Value.ToDateTime());
            serializer.Serialize(writer, dictionary);
        }

        public override object? ReadJson(
          JsonReader reader,
          Type objectType,
          object? existingValue,
          JsonSerializer serializer) => null;

        public override bool CanConvert(Type objectType) => objectType == typeof(MapField<TKey, Timestamp>) || objectType == typeof(TimeSpan);
    }
}
