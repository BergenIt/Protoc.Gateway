using Protoc.Gateway;

using Sample;
using Sample.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddGrpcClients(typeof(Greeter.GreeterClient).Assembly);
builder.Services.AddGrpc();
builder.Services.AddSwagger(builder.Configuration, typeof(Greeter.GreeterClient).Assembly, true);

WebApplication app = builder.Build();

app.UseRouting();

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("v1/swagger.yaml", "GrpcClients"));
app.MapGrpcClients();

app.MapGrpcService<GreeterService>();
app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

app.Run();
