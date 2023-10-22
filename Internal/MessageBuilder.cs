using System.Collections;
using System.Globalization;
using System.Reflection;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using Type = System.Type;

namespace Protoc.Gateway.Internal;

internal class MessageBuilder : IMessageBuilder
{
    private readonly JsonSerializerSettings _settings;

    public MessageBuilder() => _settings = JsonConvert.DefaultSettings is not null ? JsonConvert.DefaultSettings() : new JsonSerializerSettings();

    public string MessageToJson(object? message) => JsonConvert.SerializeObject(message);

    public async Task<object> BuildMessage(Type messageType, HttpContext httpContext)
    {
        object message;

        if (httpContext.Request.Method == "GET")
        {
            message = ReadQuery(httpContext.Request.Query, messageType);
        }
        else
        {
            message = await ReadBody(httpContext, messageType);
        }

        return message;
    }

    private async Task<object> ReadBody(HttpContext context, Type messageType)
    {
        context.Request.EnableBuffering();
        return JsonConvert.DeserializeObject(await new StreamReader(context.Request.Body).ReadToEndAsync(), messageType) ?? new object();
    }

    private object ReadQuery(IQueryCollection queryCollection, Type messageType)
    {
        object message = Activator.CreateInstance(messageType)
            ?? throw new InvalidOperationException(messageType.FullName);

        foreach (KeyValuePair<string, StringValues> query in queryCollection)
        {
            string[] queryKeys = query.Key.Split(".");

            for (int index = 0; index < queryKeys.Length; ++index)
            {
                Type type = message.GetType();

                string queryKey = queryKeys[index];

                string name = char.ToUpperInvariant(queryKey[0]) + queryKey[1..];

                PropertyInfo? property = type.GetProperty(name);

                if (property is not null)
                {
                    if (index == queryKeys.Length - 1)
                    {
                        if (property.PropertyType.Namespace == typeof(RepeatedField<>).Namespace)
                        {
                            Type genericArgument = property.PropertyType.GetGenericArguments()[0];

                            foreach (string? current in query.Value)
                            {
                                IList propertyValue = (IList?)property.GetValue(message)
                                    ?? throw new InvalidOperationException(property.PropertyType.FullName);

                                object? value = ParseString(genericArgument, current ?? string.Empty);

                                propertyValue.Add(value);
                            }
                            break;
                        }
                        else
                        {
                            string stringValue = query.Value.ToString();
                            object? value = ParseString(property.PropertyType, stringValue);
                            property.SetValue(message, value);
                            break;
                        }
                    }
                    else
                    {
                        object? value = property.GetValue(message);

                        if (value is null)
                        {
                            value = Activator.CreateInstance(property.PropertyType)
                                ?? throw new InvalidOperationException(property.PropertyType.FullName);

                            property.SetValue(message, value);
                        }

                        message = value;
                    }
                }
            }
        }
        return message;
    }

    private static object? ParseString(Type propertyType, string stringValue)
    {
        if (propertyType.GetInterface(nameof(IConvertible)) is null)
        {
            if (!propertyType.IsEnum)
            {
                if (propertyType != typeof(Timestamp))
                {
                    if (propertyType != typeof(Duration))
                    {
                        return JsonConvert.DeserializeObject(stringValue, propertyType);
                    }
                    else
                    {
                        return Duration.FromTimeSpan(TimeSpan.Parse(stringValue, CultureInfo.InvariantCulture));
                    }
                }
                else
                {
                    return Timestamp.FromDateTime(DateTime.Parse(stringValue, CultureInfo.InvariantCulture).ToUniversalTime());
                }
            }
            else
            {
                return System.Enum.Parse(propertyType, stringValue);
            }
        }
        else
        {
            if (!propertyType.IsEnum)
            {
                return ((IConvertible)stringValue).ToType(propertyType, CultureInfo.InvariantCulture);
            }
            else
            {
                return System.Enum.Parse(propertyType, stringValue);
            }
        }
    }
}
