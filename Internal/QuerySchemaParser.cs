using System.Reflection;

using Microsoft.OpenApi.Models;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace Protoc.Gateway.Internal;

public class QuerySchemaParser
{
    public static IList<OpenApiParameter> GetRequestQuerySchema(Type contractType, SchemaRepository repository)
    {
        List<OpenApiParameter> list = new();
        string typeComment = contractType.GetTypeComment();
        OpenApiSchema openApiSchema = repository.Schemas.GetValueOrDefault(contractType.FullName) ?? throw new InvalidOperationException(contractType.FullName);
        QueryParse(list, openApiSchema, contractType, repository, typeComment);
        return list;
    }

    private static void QueryParse(List<OpenApiParameter> openApiParameters, OpenApiSchema openApiSchema, Type contractType, SchemaRepository repository, string comment, string? parentProppertyName = null)
    {
        OpenApiSchema value;
        if (openApiSchema.Type == "Array".ToLowerInvariant() && openApiSchema.Items?.Reference != null)
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

        foreach (KeyValuePair<string, OpenApiSchema> property2 in openApiSchema.Properties)
        {
            string text = char.ToUpperInvariant(property2.Key[0]).ToString();
            string key = property2.Key;
            string name = text + key.Substring(1, key.Length - 1);
            PropertyInfo property = contractType.GetProperty(name);
            string text2 = property.GetPropertyComment();
            if (!string.IsNullOrWhiteSpace(text2))
            {
                text2 = "." + text2;
            }

            string text3 = parentProppertyName + property2.Key;
            OpenApiSchema value2 = property2.Value;
            if (property2.Value.Type == null && !repository.Schemas.TryGetValue(property2.Value.Reference.Id, out value2))
            {
                continue;
            }

            if (property2.Value.Type == "Array".ToLowerInvariant() && property2.Value.Items?.Reference != null)
            {
                if (repository.Schemas.TryGetValue(property2.Value.Items.Reference.Id, out value))
                {
                    openApiParameters.Add(new OpenApiParameter
                    {
                        In = ParameterLocation.Query,
                        Schema = value2,
                        Description = comment + text2,
                        Name = text3
                    });
                }
            }
            else if (value2.Type == "Object".ToLowerInvariant())
            {
                QueryParse(openApiParameters, value2, property.PropertyType, repository, comment + text2, text3 + ".");
            }
            else
            {
                openApiParameters.Add(new OpenApiParameter
                {
                    In = ParameterLocation.Query,
                    Schema = value2,
                    Description = comment + text2,
                    Name = text3
                });
            }
        }
    }
}
