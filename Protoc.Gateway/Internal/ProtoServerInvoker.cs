using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Runtime.CompilerServices;

using Google.Protobuf;
using Google.Protobuf.Collections;

using Grpc.Core;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

using Newtonsoft.Json.Linq;

namespace Protoc.Gateway.Internal;

internal class ProtoServerInvoker
{
    public sealed class StreamRequest<T>
    {
        public RepeatedField<T> Values { get; set; } = new RepeatedField<T>();
    }

    private readonly bool _basicMode;

    private static readonly string[] s_ignoringMeta = new string[] { "date", "server" };
    private static readonly MethodInfo s_writeGrpcStreamMethodInfo = typeof(ProtoServerInvoker)!.GetMethod("WriteGrpcStream")!;
    private static readonly MethodInfo s_writeDuplexGrpcStreamMethodInfo = typeof(ProtoServerInvoker)!.GetMethod("WriteDuplexGrpcStream")!;

    private readonly object _client;
    private readonly IEnumerable<string> _resourses;
    private readonly IMessageBuilder _messageBuilder;
    private readonly MethodInfo _methodInfo;

    public ProtoServerInvoker(IMessageBuilder messageBuilder, object client, MethodInfo methodInfo, IEnumerable<string> recourses, bool basicMode)
    {
        _basicMode = basicMode;
        _messageBuilder = messageBuilder;
        _client = client;
        _methodInfo = methodInfo;
        _resourses = recourses;
    }

    public async Task Invoke(HttpContext httpContext)
    {
        try
        {
            if (!_basicMode)
            {
                string jwt = GetJwt(httpContext);
                if (!string.IsNullOrWhiteSpace(jwt))
                {
                    JwtSecurityToken jwtSecurityToken = new(jwt);
                    if (_resourses.Any() && !jwtSecurityToken.Claims.Any((c) => _resourses.Any((r) => r == c.Type)))
                    {
                        throw new RpcException(new Status(StatusCode.PermissionDenied, "У вас нет прав на использование данного запроса"));
                    }
                }
            }

            CallOptions callOptions = new(new Metadata());
            foreach (KeyValuePair<string, StringValues> header in httpContext.Request.Headers)
            {
                string? text = (string?)header.Value;
                if (text is not null)
                {
                    callOptions.Headers?.Add(header.Key, text);
                }
            }

            Type? parameterType = _methodInfo.GetParameters()[0].ParameterType;
            Type? type = _methodInfo.ReturnParameter.ParameterType.GetProperty("ResponseStream")?.PropertyType.GetProperty("Current")?.PropertyType;
            if (type is null)
            {
                await UnaryCallHandle(httpContext, await _messageBuilder.BuildMessage(parameterType, httpContext), _methodInfo.ReturnParameter.ParameterType);
                return;
            }

            Type? responseType = type;
            if (parameterType == typeof(Metadata))
            {
                object? asyncDuplexStreamingCall = _methodInfo.Invoke(_client, new object?[]
                {
                    callOptions.Headers,
                    null,
                    CancellationToken.None
                });

                parameterType = typeof(StreamRequest<>)!.MakeGenericType(_methodInfo.ReturnType.GenericTypeArguments[0]);

                object request = await _messageBuilder.BuildMessage(parameterType, httpContext);

                await (Task)s_writeDuplexGrpcStreamMethodInfo
                    .MakeGenericMethod(_methodInfo.ReturnType.GenericTypeArguments[0], responseType)!
                    .Invoke(null, new object?[] { asyncDuplexStreamingCall, request, httpContext })!;
            }
            else
            {
                object request = await _messageBuilder.BuildMessage(parameterType, httpContext);
                object? streamValue = _methodInfo.Invoke(_client, new object[] { request, callOptions });

                await (Task)s_writeGrpcStreamMethodInfo.MakeGenericMethod(responseType)!
                    .Invoke(null, new object?[] { streamValue, httpContext })!;
            }
        }
        catch (Exception ex)
        {
            httpContext.Response.StatusCode = 500;
            string value = ex.InnerException?.Message ?? ex.Message;
            await httpContext.Response.WriteAsJsonAsync(value);
        }
    }

    public static async Task WriteGrpcStream<TMessage>(AsyncServerStreamingCall<TMessage> asyncServerStreamingCall, HttpContext httpContext) where TMessage : class, IMessage
    {
        IAsyncStreamReader<TMessage> asyncStreamReader = asyncServerStreamingCall.ResponseStream;
        await WriteGrpcResponseStream(asyncStreamReader, await asyncServerStreamingCall.ResponseHeadersAsync, httpContext);
    }

    public static async Task WriteDuplexGrpcStream<TRequest, TMessage>(AsyncDuplexStreamingCall<TRequest, TMessage> duplexStreamingCall, StreamRequest<TRequest> requests, HttpContext httpContext) where TRequest : class, IMessage where TMessage : class, IMessage
    {
        foreach (TRequest value in requests.Values)
        {
            await duplexStreamingCall.RequestStream.WriteAsync(value);
        }

        IAsyncStreamReader<TMessage> asyncStreamReader = duplexStreamingCall.ResponseStream;
        Metadata headersResult = await duplexStreamingCall.ResponseHeadersAsync;
        await duplexStreamingCall.RequestStream.CompleteAsync();
        await WriteGrpcResponseStream(asyncStreamReader, headersResult, httpContext);
    }

    private static async Task WriteGrpcResponseStream<TMessage>(IAsyncStreamReader<TMessage> asyncStreamReader, Metadata? headerResponse, HttpContext httpContext) where TMessage : class, IMessage
    {
        if (asyncStreamReader is IAsyncStreamReader<FileChunk> fileChunks)
        {
            httpContext.Response.Headers.ContentType = "application/file";

            if (headerResponse is not null)
            {
                foreach (Metadata.Entry entry in headerResponse)
                {
                    if (entry.Key == "name")
                    {
                        string text = string.IsNullOrWhiteSpace(entry.Value) ? Guid.NewGuid().ToString() : entry.Value;
                        httpContext.Response.Headers.ContentDisposition = "attachment; filename=\"" + text + "\"";
                    }
                    else
                    {
                        httpContext.Response.Headers.TryAdd(entry.Key, entry.Value);
                    }
                }
            }

            IAsyncEnumerable<FileChunk> asyncEnumerable = fileChunks.ReadAllAsync();
            await foreach (FileChunk chunk in asyncEnumerable)
            {
                await httpContext.Response.BodyWriter.WriteAsync(chunk.Chunk.Memory);
            }

            return;
        }

        httpContext.Response.Headers.ContentType = "application/json";

        JArray jArray = new();

        await foreach (TMessage message in asyncStreamReader.ReadAllAsync())
        {
            JObject item = JObject.FromObject(message);

            jArray.Add(item);
        }

        JObject jObject = new() { { "entries", jArray } };

        foreach (Metadata.Entry header in headerResponse!)
        {
            if (!s_ignoringMeta.Contains(header.Key))
            {
                jObject.Add(header.Key, (JToken)header.Value);
            }
        }

        string jsonStr = jObject.ToString();
        await httpContext.Response.WriteAsync(jsonStr);
    }

    private async Task UnaryCallHandle(HttpContext httpContent, object protoMessage, Type parameterType)
    {
        httpContent.Response.Headers.ContentType = "application/json";
        Type type = parameterType.GetProperty("ResponseAsync")?.PropertyType.GetProperty("Result")?.PropertyType!;
        PropertyInfo? metaProperty = typeof(AsyncUnaryCall<>)!.MakeGenericType(type).GetProperty("ResponseHeadersAsync")!;

        MethodInfo awaiter = typeof(AsyncUnaryCall<>)!.MakeGenericType(type).GetMethod("GetAwaiter")!;
        MethodInfo result = typeof(TaskAwaiter<>)!.MakeGenericType(type).GetMethod("GetResult")!;

        MethodInfo metaAwaiter = typeof(Task<Metadata>)!.GetMethod("GetAwaiter")!;
        MethodInfo metaResult = typeof(TaskAwaiter<Metadata>)!.GetMethod("GetResult")!;

        CallOptions callOptions = new(new Metadata());

        foreach (KeyValuePair<string, StringValues> header in httpContent.Request.Headers)
        {
            string? text = (string?)header.Value;

            if (text is not null)
            {
                callOptions.Headers?.Add(header.Key, text);
            }
        }

        object? unaryCall = _methodInfo.Invoke(_client, new object[] { protoMessage, callOptions });
        object? metaCall = metaProperty.GetValue(unaryCall);
        object? responseAwaiterObject = awaiter.Invoke(unaryCall, null);
        object? metaAwaiterObject = metaAwaiter.Invoke(metaCall, null);

        object? message = result.Invoke(responseAwaiterObject, null);
        Metadata? metadata = (Metadata?)metaResult.Invoke(metaAwaiterObject, null);

        if (metadata is not null)
        {
            foreach (Metadata.Entry item in metadata)
            {
                httpContent.Response.Headers.TryAdd(item.Key, item.Value);
            }
        }

        string responseAsJson = _messageBuilder.MessageToJson(message);
        await httpContent.Response.WriteAsync(responseAsJson);
    }

    private static string GetJwt(HttpContext context)
    {
        string text = (string?)context.Request.Headers[HeaderNames.Authorization] ?? string.Empty;

        return text.Replace("Bearer", string.Empty).Trim();
    }
}
