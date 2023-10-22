using System.Reflection;

using Microsoft.OpenApi.Models;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace Protoc.Gateway.Internal;

internal class QuerySchemaParser
{
    public static IList<OpenApiParameter> GetRequestQuerySchema(Type contractType, SchemaRepository repository)
    {
        List<OpenApiParameter> list = new();

        string typeComment = contractType.GetTypeComment();

        OpenApiSchema openApiSchema = repository.Schemas.GetValueOrDefault(contractType.FullName)
            ?? throw new InvalidOperationException(contractType.FullName);

        QueryParse(list, openApiSchema, contractType, repository, typeComment);

        return list;
    }

    private static void QueryParse(List<OpenApiParameter> openApiParameters, OpenApiSchema openApiSchema, Type contractType, SchemaRepository repository, string comment, string? parentProppertyName = null)
    {
        OpenApiSchema? value;

        if (openApiSchema.Type == "array" && openApiSchema.Items?.Reference != null)
        {
            if (repository.Schemas.TryGetValue(openApiSchema.Items.Reference.Id, out value))
            {
                openApiParameters.Add(new OpenApiParameter
                {
                    In = ParameterLocation.Query,
                    Schema = openApiSchema,
                    Description = comment,
                    Name = "values"
                });
            }

            return;
        }

        foreach (KeyValuePair<string, OpenApiSchema> schemaProperty in openApiSchema.Properties)
        {
            string firstChar = char.ToUpperInvariant(schemaProperty.Key[0]).ToString();

            string propertyKey = schemaProperty.Key;

            string name = string.Concat(firstChar, propertyKey.AsSpan(1, propertyKey.Length - 1));

            PropertyInfo? property = contractType.GetProperty(name);
            string? description = property?.GetPropertyComment();
            if (!string.IsNullOrWhiteSpace(description))
            {
                description = "." + description;
            }

            string fullName = parentProppertyName + schemaProperty.Key;

            OpenApiSchema? schemaPropertyValue = schemaProperty.Value;

            if (schemaProperty.Value.Type == null
                && !repository.Schemas.TryGetValue(schemaProperty.Value.Reference.Id, out schemaPropertyValue))
            {
                continue;
            }

            if (schemaProperty.Value.Type == "array" && schemaProperty.Value.Items?.Reference != null)
            {
                if (repository.Schemas.TryGetValue(schemaProperty.Value.Items.Reference.Id, out value))
                {
                    openApiParameters.Add(new OpenApiParameter
                    {
                        In = ParameterLocation.Query,
                        Schema = schemaPropertyValue,
                        Description = comment + description,
                        Name = fullName
                    });
                }
            }
            else if (schemaPropertyValue.Type == "object")
            {
                if (property is not null && property.PropertyType is not null)
                {
                    QueryParse(
                        openApiParameters,
                        schemaPropertyValue,
                        property.PropertyType,
                        repository,
                        comment + description,
                        fullName + ".");
                }
            }
            else
            {
                openApiParameters.Add(new OpenApiParameter
                {
                    In = ParameterLocation.Query,
                    Schema = schemaPropertyValue,
                    Description = comment + description,
                    Name = fullName
                });
            }
        }
    }
}