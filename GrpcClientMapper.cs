using Grpc.Net.ClientFactory;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Protoc.Gateway.Internal;

using System.Collections.Immutable;
using System.Reflection;

using Type = System.Type;

namespace Protoc.Gateway;

public static class GrpcClientMapper
{
    public static IServiceCollection AddGrpcClients(this WebApplicationBuilder builder, params Assembly[] assemblies)
    {
        ImmutableArray<Type> assembyTypes = assemblies
            .SelectMany(a => a.GetTypes())
            .ToImmutableArray();
        
        AssemblyParser implementationInstance = new(assembyTypes);

        builder.Services.AddSingleton<IAssemblyParser>(implementationInstance);
        builder.Services.AddSingleton<IMessageBuilder, MessageBuilder>();

        foreach (Type grpcClientType in implementationInstance.GrpcClientTypes)
        {
            Google.Protobuf.Reflection.ServiceDescriptor serviceDescriptor = (Google.Protobuf.Reflection.ServiceDescriptor)((
                grpcClientType.DeclaringType?.GetProperty("Descriptor") ?? throw new InvalidOperationException(grpcClientType.Name))
                .GetValue(null) ?? throw new InvalidOperationException(grpcClientType.Name));

            string uri = builder.Configuration.GetValue<string>(serviceDescriptor.File.Package)
                ?? throw new InvalidOperationException("Not found uri service in env: " + serviceDescriptor.File.Package);

            Action<GrpcClientFactoryOptions> action = o => o.Address = new Uri(uri);

            IHttpClientBuilder httpClientBuilder = (IHttpClientBuilder)(
                typeof(GrpcClientServiceExtensions)
                .GetMethod(
                    "AddGrpcClient",
                    new Type[2]
                    {
                        builder.Services.GetType(),
                        action.GetType()
                    }) ?? throw new InvalidOperationException($"AddGrpcClient: {grpcClientType.Name}"))
                .MakeGenericMethod(grpcClientType)
                .Invoke(
                    null,
                    new object[2]
                    {
                        builder.Services,
                        action
                    })!;

            httpClientBuilder.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
            {
                ServerCertificateCustomValidationCallback = (r, c, ch, e) => ch != null && !ch.ChainStatus.Any()
            });
        }

        return builder.Services;
    }

    public static IApplicationBuilder MapGrpcClients(this IApplicationBuilder applicationBuilder, bool basicMode = false)
    {
        using IServiceScope serviceScope = applicationBuilder.ApplicationServices.CreateScope();

        IAssemblyParser assemblyParser = serviceScope.ServiceProvider.GetRequiredService<IAssemblyParser>();
        IMessageBuilder messageBuilder = serviceScope.ServiceProvider.GetRequiredService<IMessageBuilder>();

        return applicationBuilder.UseEndpoints(endpoints =>
        {
            foreach (Type grpcClientType in assemblyParser.GrpcClientTypes)
            {
                object requiredService = serviceScope.ServiceProvider.GetRequiredService(grpcClientType);

                foreach (MethodInfo grpcMethod in assemblyParser.GetGrpcMethods(grpcClientType))
                {
                    Methods methods = assemblyParser.GetHttpMethods(grpcMethod, out string methodName);

                    IEnumerable<string> resources = assemblyParser.GetResources(grpcMethod, out string _);

                    ProtoServerInvoker protoServerInvoker = new(messageBuilder, requiredService, grpcMethod, resources, basicMode);

                    string pattern = "protobuf/" + grpcClientType.DeclaringType?.Name + "/" + methodName;

                    if (methods == Methods.File)
                    {
                        methods = Methods.Get;
                    }

                    endpoints.MapMethods(
                        pattern,
                        new string[1] { methods.ToString().ToUpperInvariant() },
                        new RequestDelegate(protoServerInvoker.Invoke));
                }
            }
        });
    }
}
