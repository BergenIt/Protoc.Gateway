using Microsoft.AspNetCore.Http;

using Type = System.Type;

namespace Protoc.Gateway;

public interface IMessageBuilder
{
    Task<object> BuildMessage(Type messageType, HttpContext httpContext);

    string MessageToJson(object? message);
}
