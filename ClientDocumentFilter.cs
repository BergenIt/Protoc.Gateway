﻿using Google.Protobuf.Reflection;
using Google.Protobuf.WellKnownTypes;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.Extensions.Configuration;

using System.Reflection;

using Type = System.Type;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Protoc.Gateway;

public class ClientDocumentFilter : IDocumentFilter
{
    private readonly IAssemblyParser _assemblyParser;
    private readonly IConfiguration _configuration;

    public ClientDocumentFilter(IAssemblyParser assemblyParser, IConfiguration configuration)
    {
        _assemblyParser = assemblyParser;
        _configuration = configuration;
    }

    void IDocumentFilter.Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        context.SchemaGenerator.GenerateSchema(typeof(Empty), context.SchemaRepository);

        foreach (TypeInfo modelType in _assemblyParser.GrpcTypes.Cast<TypeInfo>())
        {
            context.SchemaGenerator.GenerateSchema(modelType, context.SchemaRepository);

            context.SchemaRepository.AddDefinition(
                typeof(ICollection<>).MakeGenericType(modelType).FullName,
                context.SchemaGenerator.GenerateSchema(
                    typeof(ICollection<>).MakeGenericType(modelType),
                    context.SchemaRepository));
        }

        foreach (KeyValuePair<string, OpenApiSchema> schema in context.SchemaRepository.Schemas)
        {
            SetReadonlyFalse(schema.Value);
        }

        foreach (Type grpcClientType in _assemblyParser.GrpcClientTypes)
        {
            IEnumerable<MethodInfo> grpcMethods = _assemblyParser.GetGrpcMethods(grpcClientType);
            string service = grpcClientType.DeclaringType?.Name ?? throw new InvalidOperationException(grpcClientType.Name);
            string clientComment = grpcClientType.GetClientComment();

            FileDescriptor? serviceFile = _assemblyParser.GetServiceFile(grpcClientType.DeclaringType);

            swaggerDoc.Tags.Add(new()
            {
                Name = service,
                Description = clientComment,
                ExternalDocs = new()
                {
                    Description = serviceFile?.Name,
                    Url = new(_configuration?.GetValue<string>(serviceFile?.Package!)!)
                }
            });

            foreach (MethodInfo methodInfo in grpcMethods)
            {
                GenerateOperation(methodInfo, context.SchemaRepository, service, out string endpoint, out OpenApiPathItem? openApiPathItem);

                if (openApiPathItem != null)
                {
                    swaggerDoc.Paths.Add(endpoint, openApiPathItem);
                }
            }
        }
    }

    private void GenerateOperation(
        MethodInfo methodInfo,
        SchemaRepository repository,
        string service,
        out string endpoint,
        out OpenApiPathItem? openApiPathItem)
    {
        Methods httpMethods = _assemblyParser.GetHttpMethods(methodInfo, out string methodName);
        endpoint = "/protobuf/" + service + "/" + methodName;

        Type parameterType1 = methodInfo.ReturnParameter.ParameterType;
        Type? propertyType = parameterType1.GetProperty("ResponseStream")?.PropertyType.GetProperty("Current")?.PropertyType;
        Type? messageType = parameterType1.GetProperty("ResponseAsync")?.PropertyType.GetProperty("Result")?.PropertyType;

        messageType ??= propertyType ?? throw new InvalidOperationException(methodInfo.Name);

        string[] source = methodInfo.GetMethodComment().Split('\n', StringSplitOptions.RemoveEmptyEntries);

        OpenApiOperation operation = new()
        {
            Description = source.Skip(1).FirstOrDefault() ?? string.Empty,
            Summary = source.FirstOrDefault() ?? string.Empty
        };

        operation.Responses.Add("200", GetOpenApiResponse(messageType, repository));
        operation.Tags.Add(new OpenApiTag()
        {
            Name = service
        });

        if (httpMethods == Methods.Get || httpMethods == Methods.File)
        {
            Type methodParameterType = methodInfo.GetParameters()[0].ParameterType;

            if (methodParameterType == typeof(Metadata))
            {
                operation.Parameters = QuerySchemaParser.GetRequestQuerySchema(
                    typeof(ICollection<>).MakeGenericType(methodInfo.ReturnType.GenericTypeArguments[0]),
                    repository);
            }
            else
            {
                operation.Parameters = QuerySchemaParser.GetRequestQuerySchema(methodParameterType, repository);
            }
        }
        else
        {
            OpenApiRequestBody? openApiRequestBody = GetOpenApiRequestBody(methodInfo, repository);

            if (openApiRequestBody == null)
            {
                openApiPathItem = null;
                return;
            }

            operation.RequestBody = openApiRequestBody;
        }

        openApiPathItem = new()
        {
            Operations =
            {
                {
                    httpMethods == Methods.File ? OperationType.Get : System.Enum.Parse<OperationType>(httpMethods.ToString()),
                    operation
                }
            }
        };
    }

    private static OpenApiResponse GetOpenApiResponse(Type messageType, SchemaRepository repository)
    {
        OpenApiResponse openApiResponse = new();

        OpenApiSchema openApiSchema = repository.Schemas.GetValueOrDefault(messageType.FullName)
            ?? throw new InvalidOperationException(messageType.FullName);

        openApiResponse.Content.Add("application/json", new OpenApiMediaType()
        {
            Schema = openApiSchema
        });

        openApiResponse.Description = "Success";

        return openApiResponse;
    }

    private static OpenApiRequestBody? GetOpenApiRequestBody(
      MethodInfo method,
      SchemaRepository repository)
    {
        TypeInfo parameterType = (TypeInfo)method.GetParameters()[0].ParameterType;

        if (!parameterType.ImplementedInterfaces.Contains(typeof(IMessage)))
        {
            return null;
        }

        OpenApiSchema openApiSchema = repository.Schemas.GetValueOrDefault(parameterType.FullName)
            ?? throw new InvalidOperationException(parameterType.FullName);

        return new()
        {
            Content = { { "application/json", new() { Schema = openApiSchema } } }
        };
    }

    private static void SetReadonlyFalse(OpenApiSchema openApiSchema)
    {
        openApiSchema.ReadOnly = false;

        foreach (KeyValuePair<string, OpenApiSchema> property in openApiSchema.Properties)
        {
            SetReadonlyFalse(property.Value);
        }
    }
}
