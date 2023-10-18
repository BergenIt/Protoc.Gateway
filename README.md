# Protoc.Gateway

Runtime gateway library from grpc to rest.

!> В разработке

## Что это

Библиотека, которая парсит ассамблею, находит все `grpc-клиентов` и создает `rest-обработчиков` под каждый найденный `rpc`.

Поддерживает все типы `rpc`:

* `unary`
* `server-stream`
* `client-stream`
* `duplex-stream`

Поддерживает функционал авторизации в 3 вариациях:

* `jwt` (с возможностью ограничения доступа к `rpc` на основании клаймов в токене)
* `basic`
* `none`

Также генерирует `swagger`(`openapi`) для всех намапленных в `rest` `rpc`.

## А зачем это нужно

Мы ее используем в 3 сценариях:

* Базис для `api-gw` решения
* Для локальной отладки при разработке `grpc-сервисов` через `swagger`
* Для автоматического тестирования `api` `grpc-сервисов`

## А как этим пользоваться

Устанавливаем [`nuget` пакет](https://www.nuget.org/packages/Protoc.Gateway)

Добавляем `grpc-клиентов` в `di`:

```csharp
builder.AddGrpcClients(typeof(MyGrpcExampleService).Assembly);
```

Где `MyGrpcExampleService` - любой тип из ассамблеи, содержащей `grpc` сервисы.

Добавляем `swagger`, собранный из этих этих `grpc-клиентов`:

```csharp
builder.Services.AddSwagger(builder.Configuration, typeof(MyGrpcExampleService).Assembly, true);
```

Где `MyGrpcExampleService` - любой тип из ассамблеи, содержащей `xml` документацию для сгенерированных `rpc`.

Объявляем обработчиков для всех `rpc` и `swagger`:

```csharp
app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("v1/swagger.yaml", "GrpcClients"));
app.MapGrpcClients();
```
