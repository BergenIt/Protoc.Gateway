using Grpc.Core;

namespace Sample.Services;

/// <summary>
/// Базовый сервис для тестов
/// </summary>
public class GreeterService : Greeter.GreeterBase
{
    private readonly ILogger<GreeterService> _logger;

    /// <summary>
    /// Базовый сервис для тестов
    /// </summary>
    /// <param name="logger"></param>
    public GreeterService(ILogger<GreeterService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Hello 1
    /// </summary>
    /// <param name="request"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    public override Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context)
    {
        _logger.LogInformation("hello !!!");

        return Task.FromResult(new HelloReply
        {
            Message = "Hello " + request.Name
        });
    }

    /// <summary>
    /// Hello 2
    /// </summary>
    /// <param name="request"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    public override Task<HelloReply> SayHelloDefault(HelloRequest request, ServerCallContext context)
    {
        _logger.LogInformation("hello 2 !!!");

        return Task.FromResult(new HelloReply
        {
            Message = "Hello " + request.Name
        });
    }
}