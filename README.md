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

* Базис для `rest-gateway` решения в `grpc` окружении
* Для локальной отладки при разработке `grpc-сервисов` через `swagger`
* Для автоматического тестирования `api` `grpc-сервисов` с помощью `rest`

## А как этим пользоваться

### Настройка проекта

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

Метод `AddSwagger` принимает булевый параметр `basicMode` - на основании этого параметра в `swagger` добавляется возможность ввода логина/пароля для basic-авторизации (значение `true`) или возможность ввода `jwt` (значение `false`) .

Где `MyGrpcExampleService` - любой тип из ассамблеи, содержащей `xml` документацию для сгенерированных `rpc`.

Объявляем обработчиков для всех `rpc` и `swagger`:

```csharp
app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("v1/swagger.yaml", "GrpcClients"));
app.MapGrpcClients();
```

В файл `appsettings.json` или в переменные окружения добавляем переменную, где ключ - название пакета прото, а значение - адрес `grpc-сервера`, реализующего этот прото, в формате '<http://host:port>'.

Например:

```env
"greet": "https://localhost:7186/"
```

### Настройка proto

Возле директории с вашими прото создаем файл - `api.proto`, с следующим контеном:

```proto
syntax = "proto3";
package api;

option csharp_namespace = "Protoc.Gateway";

import "google/protobuf/descriptor.proto";

//{option(Method) = Get;}
//{option(Resource) = "test";}

extend google.protobuf.MethodOptions {
    Methods method = 1024; // Метод для мапинга в api 
    repeated string resource = 2048; // Ресурс для доступа к методу
}

extend google.protobuf.ServiceOptions {
    repeated string service_resource = 2048; // Ресурсы для доступа к сервису
}

/*
 * Перечисление с доступными http методами
 */
enum Methods {
    GET = 0; // http get
    POST = 1; // http post
    PUT = 2; // http put
    PATCH = 3; // http patch
    DELETE = 4; // http delete
    FILE = 5; // http file content 
}

// Чанк файла
message FileChunk {
    bytes chunk = 1; // Чанк размером 1024 (максимум)
}
```

Импортируем его в ваши файлы с `grpc-сервисами`, объявляем в опциях `rpc` желаемые `http-методы` (по умолчанию - `POST`):

При желании ограничения доступа к сервисам или `rpc` на основании клаймов в `jwt` добавьте опции resource, где в качестве значения представлен клайм, который должен присутсвовать в токене.

Полный пример:

```proto
syntax = "proto3";
package greet;

import "api.proto";

service Greeter {
  option(api.service_resource) = "greeter_service";
  option(api.service_resource) = "greeter_service2";

  rpc SayHello (HelloRequest) returns (HelloReply) {
      option(api.method) = GET;
      option(api.resource) = "hellower";
      option(api.resource) = "hellower2";
    }
}

message HelloRequest {
  string name = 1;
}

message HelloReply {
  string message = 1;
}
```

## Роадмап

* [ ] Описать как использовать `FileChunk`
* [ ] Избавиться от костыля с необходимость вставки `api.proto` (если кто знает - помогите)
* [ ] Доработать функционал обработки клаймов для ввода уровня доступа в качестве значения клайма (`readonly`, `read+create`, `full-access`) на основании `http` метода.
* [ ] Перевести все это на кодогенерацию (в далеком будущем)
