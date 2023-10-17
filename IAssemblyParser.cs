using Google.Protobuf.Reflection;

using System.Reflection;

namespace Protoc.Gateway;

public interface IAssemblyParser
{
    IEnumerable<Type> GrpcTypes { get; }

    IEnumerable<Type> GrpcClientTypes { get; }

    Methods GetHttpMethods(MethodInfo methodInfo, out string methodName);

    IEnumerable<string> GetResources(MethodInfo methodInfo, out string methodName);

    IEnumerable<MethodInfo> GetGrpcMethods(Type clientType);

    FileDescriptor? GetServiceFile(Type serviceType);
}
