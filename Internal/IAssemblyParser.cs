using System.Reflection;
using Google.Protobuf.Reflection;

namespace Protoc.Gateway.Internal;

internal interface IAssemblyParser
{
    Methods GetHttpMethods(MethodInfo methodInfo, out string methodName);

    IReadOnlyCollection<Type> GrpcTypes { get; }

    IReadOnlyCollection<Type> GrpcClientTypes { get; }

    IReadOnlyCollection<string> GetResources(MethodInfo methodInfo, out string methodName);

    IReadOnlyCollection<MethodInfo> GetGrpcMethods(Type clientType);

    FileDescriptor? GetServiceFile(Type serviceType);
}
