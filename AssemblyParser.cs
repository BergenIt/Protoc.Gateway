using Google.Protobuf.Collections;
using Google.Protobuf.Reflection;
using Google.Protobuf;
using System.Reflection;
using Grpc.Core;

namespace Protoc.Gateway;

public class AssemblyParser : IAssemblyParser
{
    public AssemblyParser(IEnumerable<Type> types)
    {
        GrpcClientTypes = GetType().Assembly.GetTypes().Concat(types).Where(t =>
        {
            int genLen = t.BaseType is not null ? t.BaseType?.GenericTypeArguments.Length == 1 ? 1 : 0 : 0;

            return genLen != 0
                && t.BaseType?.GenericTypeArguments.Single() == t
                && t.BaseType.BaseType == typeof(ClientBase);
        });

        GrpcTypes = GetType().Assembly.GetTypes().Concat(types).Where(t => ((TypeInfo)t).ImplementedInterfaces.Contains(typeof(IMessage)));
    }

    public IEnumerable<Type> GrpcClientTypes { get; }

    public IEnumerable<Type> GrpcTypes { get; }

    public IEnumerable<MethodInfo> GetGrpcMethods(Type clientType) => clientType
        .GetMethods()
        .Where(m => m.ReturnType.IsGenericType)
        .Where(m =>
        {
            bool isDuplexMethod = m.ReturnType.GenericTypeArguments.Length == 2
                && m.ReturnType == typeof(AsyncDuplexStreamingCall<,>).MakeGenericType(m.ReturnType.GenericTypeArguments[0], m.ReturnType.GenericTypeArguments[1])
                && m.GetParameters().Length == 3;

            if (isDuplexMethod)
            {
                return true;
            }

            bool isNotUnaryCall = m.ReturnType != typeof(AsyncUnaryCall<>).MakeGenericType(m.ReturnType.GenericTypeArguments[0]) || m.GetParameters().Length != 2;

            if (isNotUnaryCall)
            {
                return m.ReturnType == typeof(AsyncServerStreamingCall<>).MakeGenericType(m.ReturnType.GenericTypeArguments[0]) && m.GetParameters().Length == 2;
            }

            return true;
        });

    public Methods GetHttpMethods(MethodInfo methodInfo, out string methodName) => GetMethod(methodInfo, out methodName)?
        .GetOptions()?
        .GetExtension(new Extension<MethodOptions, Methods>(
            1024,
            FieldCodec.ForEnum(
                1024U,
                e => (int)e,
                i => (Methods)i, Methods.Post)))
        ?? Methods.Post;

    public IEnumerable<string> GetResources(MethodInfo methodInfo, out string methodName)
    {
        ServiceDescriptor? serviceDescriptor = GetServiceDescriptor(methodInfo.DeclaringType?.DeclaringType
            ?? throw new InvalidOperationException(methodInfo.Name));

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
            .GetOptions()
            .GetExtension(new RepeatedExtension<MethodOptions, string>(
                2048,
                FieldCodec.ForString(
                    2048U,
                    string.Empty)))
            ?? new());

        return resources;
    }

    public FileDescriptor? GetServiceFile(Type serviceType) => GetServiceDescriptor(serviceType)?.File;

    private static ServiceDescriptor? GetServiceDescriptor(Type serviceType) => (ServiceDescriptor)((serviceType.GetProperty("Descriptor") ?? throw new InvalidOperationException(serviceType.Name)).GetValue(null) ?? throw new InvalidOperationException(serviceType.Name));

    private static MethodDescriptor? GetMethod(MethodInfo methodInfo, out string methodName)
    {
        methodName = methodInfo.Name.Replace("Async", string.Empty);

        PropertyInfo? property = methodInfo.DeclaringType?.DeclaringType?.GetProperty("Descriptor");

        object obj = (property is not null
            ? property.GetValue(null)
            : throw new InvalidOperationException(methodInfo.Name))
        ?? throw new InvalidOperationException(methodInfo.Name);

        return (MethodDescriptor?)(property.PropertyType.GetMethod("FindMethodByName")
            ?? throw new InvalidOperationException(methodName)).Invoke(obj, new object[1] { methodName });
    }
}
