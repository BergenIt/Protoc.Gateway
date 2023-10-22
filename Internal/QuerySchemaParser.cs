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

        QueryParse(
            list,
            openApiSchema,
            contractType,
            repository,
            typeComment);

        return list;
    }

    private static void QueryParse(List<OpenApiParameter> openApiParameters, OpenApiSchema openApiSchema, Type contractType, SchemaRepository repository, string comment, string? parentProppertyName = null)
    {
        if (openApiSchema.Type == "array" && openApiSchema.Items?.Reference != null)
        {
            if (repository.Schemas.TryGetValue(openApiSchema.Items.Reference.Id, out _))
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

        foreach (KeyValuePair<string, OpenApiSchema> propKv in openApiSchema.Properties)
        {
            string key = propKv.Key;

            string name = char.ToUpperInvariant(key[0]) + key[1..];

            string? description = contractType
                .GetProperty(name)?
                .GetPropertyComment();

            if (!string.IsNullOrWhiteSpace(description))
            {
                description = "." + description;
            }

            OpenApiSchema? schema = propKv.Value;

            if (propKv.Value.Type == null && !repository.Schemas.TryGetValue(propKv.Value.Reference.Id, out schema))
            {
                continue;
            }

            if (propKv.Value.Type == "array" && propKv.Value.Items?.Reference is not null)
            {
                if (repository.Schemas.TryGetValue(propKv.Value.Items.Reference.Id, out _))
                {
                    openApiParameters.Add(new OpenApiParameter
                    {
                        In = ParameterLocation.Query,
                        Schema = schema,
                        Description = comment + description,
                        Name = parentProppertyName + propKv.Key
                    });
                }

                return;
            }

            if (schema.Type == "object" && contractType.GetProperty(name)?.PropertyType is Type propertyType)
            {
                QueryParse(
                    openApiParameters,
                    schema,
                    propertyType,
                    repository,
                    comment + description,
                    parentProppertyName + propKv.Key + ".");

                return;
            }

            openApiParameters.Add(new OpenApiParameter
            {
                In = ParameterLocation.Query,
                Schema = schema,
                Description = comment + description,
                Name = parentProppertyName + propKv.Key
            });
        }
    }
}
