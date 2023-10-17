using System.Reflection;

using Type = System.Type;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Protoc.Gateway;

public class QuerySchemaParser
{
    public static IList<OpenApiParameter> GetRequestQuerySchema(
      Type contractType,
      SchemaRepository repository)
    {
        List<OpenApiParameter> openApiParameters = new();
        string typeComment = contractType.GetTypeComment();
        QueryParse(openApiParameters, repository.Schemas.GetValueOrDefault(contractType.FullName) ?? throw new InvalidOperationException(contractType.FullName), contractType, repository, typeComment);
        return openApiParameters;
    }

    private static void QueryParse(
      List<OpenApiParameter> openApiParameters,
      OpenApiSchema openApiSchema,
      Type contractType,
      SchemaRepository repository,
      string comment,
      string? parentProppertyName = null)
    {
        if (openApiSchema.Type == "array" && openApiSchema.Items?.Reference != null)
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
            foreach (KeyValuePair<string, OpenApiSchema> property1 in (IEnumerable<KeyValuePair<string, OpenApiSchema>>)openApiSchema.Properties)
            {
                string str1 = char.ToUpperInvariant(property1.Key[0]).ToString();
                string key = property1.Key;
                string str2 = key.Substring(1, key.Length - 1);
                string name = str1 + str2;
                PropertyInfo property2 = contractType.GetProperty(name)!;
                string str3 = property2.GetPropertyComment();

                if (!string.IsNullOrWhiteSpace(str3))
                {
                    str3 = "." + str3;
                }

                if (property1.Value.Type != null || repository.Schemas.TryGetValue(property1.Value.Reference.Id, out _))
                {
                    if (property1.Value.Type == "Array".ToLowerInvariant() && property1.Value.Items?.Reference != null)
                    {
                        if (repository.Schemas.TryGetValue(property1.Value.Items.Reference.Id, out _))
                        {
                            openApiParameters.Add(new OpenApiParameter()
                            {
                                In = new ParameterLocation?(ParameterLocation.Query),
                                Schema = property1.Value,
                                Description = comment + str3,
                                Name = parentProppertyName + property1.Key
                            });
                        }
                    }
                    else if (property1.Value.Type == "Object".ToLowerInvariant())
                    {
                        QueryParse(openApiParameters, property1.Value, property2.PropertyType, repository, comment + str3, parentProppertyName + property1.Key + ".");
                    }
                    else
                    {
                        openApiParameters.Add(new OpenApiParameter()
                        {
                            In = new ParameterLocation?(ParameterLocation.Query),
                            Schema = property1.Value,
                            Description = comment + str3,
                            Name = parentProppertyName + property1.Key
                        });
                    }
                }
            }
        }
    }
}
