using System.Reflection;

using Type = System.Type;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Protoc.Gateway.Internal;

public static class QuerySchemaParser
{
    public static IList<OpenApiParameter> GetRequestQuerySchema(Type contractType, SchemaRepository repository)
    {
        OpenApiSchema openApiSchema = repository.Schemas.GetValueOrDefault(contractType.FullName)
            ?? throw new InvalidOperationException(contractType.FullName);

        string typeComment = contractType.GetTypeComment();

        List<OpenApiParameter> openApiParameters = new();

        QueryParse(openApiParameters, openApiSchema, contractType, repository, typeComment);

        return openApiParameters;
    }

    private static void QueryParse(
      List<OpenApiParameter> openApiParameters,
      OpenApiSchema openApiSchema,
      Type contractType,
      SchemaRepository repository,
      string comment,
      string? parentPropertyName = null)
    {
        if (openApiSchema.Type == "array" && openApiSchema.Items?.Reference is not null)
        {
            if (!repository.Schemas.TryGetValue(openApiSchema.Items.Reference.Id, out _))
            {
                return;
            }

            openApiParameters.Add(new OpenApiParameter()
            {
                In = new ParameterLocation?(ParameterLocation.Query),
                Schema = openApiSchema,
                Description = comment,
                Name = "values"
            });
        }
        else
        {
            foreach (KeyValuePair<string, OpenApiSchema> item in openApiSchema.Properties)
            {
                string name = char.ToUpperInvariant(item.Key[0]).ToString() + item.Key[1..];

                PropertyInfo propertyType = contractType.GetProperty(name)!;

                string propertyComment = propertyType.GetPropertyComment();

                if (!string.IsNullOrWhiteSpace(propertyComment))
                {
                    propertyComment = "." + propertyComment;
                }

                if (item.Value.Type is not null || repository.Schemas.TryGetValue(item.Value.Reference.Id, out _))
                {
                    if (item.Value.Type == "array" && item.Value.Items?.Reference is not null)
                    {
                        if (repository.Schemas.TryGetValue(item.Value.Items.Reference.Id, out _))
                        {
                            openApiParameters.Add(new()
                            {
                                In = new ParameterLocation?(ParameterLocation.Query),
                                Schema = item.Value,
                                Description = comment + propertyComment,
                                Name = parentPropertyName + item.Key
                            });
                        }
                    }
                    else if (item.Value.Type == "object")
                    {
                        QueryParse(
                            openApiParameters,
                            item.Value,
                            propertyType.PropertyType,
                            repository,
                            comment + propertyComment,
                            parentPropertyName + item.Key + ".");
                    }
                    else
                    {
                        openApiParameters.Add(new()
                        {
                            In = new ParameterLocation?(ParameterLocation.Query),
                            Schema = item.Value,
                            Description = comment + propertyComment,
                            Name = parentPropertyName + item.Key
                        });
                    }
                }
            }
        }
    }
}
