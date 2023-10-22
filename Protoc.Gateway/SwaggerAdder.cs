using System.Reflection;
using System.Text.Json.Serialization;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using Protoc.Gateway.Internal;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Protoc.Gateway;

public static class SwaggerAdder
{
    public static IServiceCollection AddSwagger(
      this IServiceCollection serviceDescriptors,
      IConfiguration configuration,
      Assembly? executingAssembly = null,
      bool basicMode = false,
      Action<SwaggerGenOptions>? setupAction = null)
    {
        Func<JsonSerializerSettings>? defaultSettings = JsonConvert.DefaultSettings;
        JsonSerializerSettings overSettings = (defaultSettings is not null ? defaultSettings() : null) ?? new JsonSerializerSettings();
        JsonConvert.DefaultSettings = () =>
        {
            overSettings.ContractResolver = new ProtobufTimeResolver();
            return overSettings;
        };
        serviceDescriptors.AddEndpointsApiExplorer();
        serviceDescriptors.AddSingleton(s => new ClientDocumentFilter(s.GetRequiredService<IAssemblyParser>(), configuration));
        serviceDescriptors.AddControllers().AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        serviceDescriptors.AddSwaggerGen(a =>
        {
            if (executingAssembly is not null)
            {
                string filePath = Path.Combine(AppContext.BaseDirectory, executingAssembly.GetName().Name + ".xml");
                a.IncludeXmlComments(filePath);
            }
            OpenApiSecurityScheme securityScheme = new()
            {
                Description = (basicMode ? "Basic" : "Jwt") + " Authorization header using the Bearer scheme.",
                Name = HeaderNames.Authorization,
                In = ParameterLocation.Header,
                Scheme = basicMode ? "Basic" : "Bearer",
                Type = SecuritySchemeType.Http
            };
            if (!basicMode)
            {
                securityScheme.BearerFormat = "JWT";
            }

            a.AddSecurityDefinition(basicMode ? "Basic" : "Bearer", securityScheme);
            SwaggerGenOptions swaggerGenOptions = a;
            swaggerGenOptions.AddSecurityRequirement(new OpenApiSecurityRequirement()
        {
          {
            new OpenApiSecurityScheme()
            {
              Reference = new OpenApiReference()
              {
                Type = new ReferenceType?(ReferenceType.SecurityScheme),
                Id = basicMode ? "Basic" : "Bearer"
              }
            },
            (IList<string>) new List<string>()
          }
        });
            a.SchemaGeneratorOptions.CustomTypeMappings.Add(typeof(Timestamp), () => new OpenApiSchema()
            {
                Type = "string",
                Format = "date-time"
            });
            a.SchemaGeneratorOptions.CustomTypeMappings.Add(typeof(Duration), () => new OpenApiSchema()
            {
                Type = "string",
                Format = "duration"
            });
            a.SchemaGeneratorOptions.SchemaIdSelector = t => t.FullName;
            a.DocumentFilter<ClientDocumentFilter>();
            if (setupAction is null)
            {
                return;
            }

            setupAction(a);
        });
        return serviceDescriptors;
    }
}
