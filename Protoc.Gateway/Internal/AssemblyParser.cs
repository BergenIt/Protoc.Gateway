using System.Collections.Immutable;
using System.Reflection;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Google.Protobuf.Reflection;
using Grpc.Core;

namespace Protoc.Gateway.Internal;

internal class AssemblyParser : IAssemblyParser
{
    public IReadOnlyCollection<Type> GrpcClientTypes { get; }
    public IReadOnlyCollection<Type> GrpcTypes { get; }

    public AssemblyParser(IReadOnlyCollection<Type> types)
    {
        GrpcClientTypes = typeof(AssemblyParser).Assembly
            .GetTypes()
            .Concat(types)
            .Where(t => t.BaseType is not null
                && t.BaseType?.GenericTypeArguments.Length == 1
                && t.BaseType?.GenericTypeArguments.Single() == t
                && t.BaseType.BaseType == typeof(ClientBase))
            .ToImmutableArray();

        GrpcTypes = GetType().Assembly
            .GetTypes()
            .Concat(types)
            .Where(t => ((TypeInfo)t).ImplementedInterfaces.Contains(typeof(IMessage)))
            .ToImmutableArray();
    }

    public IReadOnlyCollection<MethodInfo> GetGrpcMethods(Type clientType)
    {
        return clientType
            .GetMethods()
            .Where(m => m.ReturnType.IsGenericType)
            .Where(m =>
            {
                bool isDuplexMethod = m.ReturnType.GenericTypeArguments.Length == 2
                    && m.ReturnType == typeof(AsyncDuplexStreamingCall<,>)
                        .MakeGenericType(
                            m.ReturnType.GenericTypeArguments[0],
                            m.ReturnType.GenericTypeArguments[1])
                    && m.GetParameters().Length == 3;

                if (isDuplexMethod)
                {
                    return true;
                }

                bool isNotUnaryCall = m.ReturnType != typeof(AsyncUnaryCall<>)
                    .MakeGenericType(m.ReturnType.GenericTypeArguments[0])
                    || m.GetParameters().Length != 2;

                if (isNotUnaryCall)
                {
                    return m.ReturnType == typeof(AsyncServerStreamingCall<>)
                        .MakeGenericType(m.ReturnType.GenericTypeArguments[0])
                        && m.GetParameters().Length == 2;
                }

                return true;
            })
            .ToImmutableArray();
    }

    public Methods GetHttpMethods(MethodInfo methodInfo, out string methodName)
    {
        return GetMethod(methodInfo, out methodName)?
            .GetOptions()?
            .GetExtension(new Extension<MethodOptions, Methods>(
                1024,
                FieldCodec.ForEnum(
                    1024U,
                    e => (int)e,
                    i => (Methods)i, Methods.Post)))
            ?? Methods.Post;
    }

    public IReadOnlyCollection<string> GetResources(MethodInfo methodInfo, out string methodName)
    {
        Type serviceType = methodInfo.DeclaringType?.DeclaringType
            ?? throw new InvalidOperationException(methodInfo.Name);

        ServiceDescriptor? serviceDescriptor = GetServiceDescriptor(serviceType);

        MethodDescriptor? method = GetMethod(methodInfo, out methodName);

        RepeatedField<string> resources = serviceDescriptor?
            .GetOptions()?
            .GetExtension(new RepeatedExtension<ServiceOptions, string>(
                2048,
                FieldCodec.ForString(
                    2048U,
                    string.Empty)))
            ?? new();

        resources.AddRange(method?
            .GetOptions()?
            .GetExtension(new RepeatedExtension<MethodOptions, string>(
                2048,
                FieldCodec.ForString(
                    2048U,
                    string.Empty)))
            ?? new());

        return resources;
    }

    public FileDescriptor? GetServiceFile(Type serviceType)
    {
        return GetServiceDescriptor(serviceType)?.File;
    }

    private static ServiceDescriptor? GetServiceDescriptor(Type serviceType)
    {
        PropertyInfo descriptorInfo = serviceType.GetProperty("Descriptor")
            ?? throw new InvalidOperationException(serviceType.Name);

        return (ServiceDescriptor?)descriptorInfo.GetValue(null)
            ?? throw new InvalidOperationException(serviceType.Name);
    }

    private static MethodDescriptor? GetMethod(MethodInfo methodInfo, out string methodName)
    {
        methodName = methodInfo.Name.Replace("Async", string.Empty);

        PropertyInfo property = methodInfo.DeclaringType?.DeclaringType?.GetProperty("Descriptor")
            ?? throw new InvalidOperationException(methodInfo.Name);

        object descriptor = property.GetValue(null)
            ?? throw new InvalidOperationException(methodInfo.Name);

        MethodInfo findMethodByName = property.PropertyType.GetMethod("FindMethodByName")
            ?? throw new InvalidOperationException(methodName);

        return (MethodDescriptor?)findMethodByName.Invoke(
            descriptor,
            new object[1] { methodName });
    }
}
