using Microsoft.AspNetCore.Http;

using Type = System.Type;

namespace Protoc.Gateway.Internal;

internal interface IMessageBuilder
{
    Task<object> BuildMessage(Type messageType, HttpContext httpContext);

    string MessageToJson(object? message);
}
