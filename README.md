# Easy Request Handler

**EasyRequestHandler** is a lightweight and extensible .NET library that simplifies request and event handling using patterns like Mediator and Event Dispatcher. It integrates seamlessly with the .NET Dependency Injection (DI) system and supports asynchronous operations, making it ideal for modular applications and event-driven systems.

## ✨ Features

- **Mediator Pattern**: Centralizes request handling with support for request pre/post hooks and behaviors (middleware).
- **Flexible Request Handling**: Use `RequestHandler<TRequest, TResponse>` or `RequestHandler<TResponse>` base classes.
- **Event Dispatching**: Handle and publish events using `IEventHandler<TEvent>`.
- **Automatic Registration**: Register all handlers with a single method using `IServiceCollection` extensions.

## 📦 Installation

Install from NuGet using the following command:

```
Install-Package EasyRequestHandler
```

## 🚀 Usage

### 🧭 Request Handling

#### Basic Request Handlers
Handlers can be used in two ways:

- **Direct Injection**: Inject the handler class itself (e.g., `MyRequestHandler`) into your controller or service and call `HandleAsync()` directly.
- **Mediator Pattern**: Use the `ISender` interface to decouple request handling and improve testability and separation of concerns.

Here’s how you could invoke a request using the `ISender` interface:

```csharp
public class MyApiController : ControllerBase
{
    private readonly ISender _sender;

    public MyApiController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("double")]
    public async Task<IActionResult> GetDoubledNumber([FromQuery] int number)
    {
        var response = await _sender.SendAsync<MyRequest, MyResponse>(new MyRequest { Number = number });
        return Ok(response.Result);
    }
}
```


Define a request and handler:

```csharp
public class MyRequest
{
    public int Number { get; set; }
}

public class MyResponse
{
    public int Result { get; set; }
}

public class MyRequestHandler : RequestHandler<MyRequest, MyResponse>
{
    public override Task<MyResponse> HandleAsync(MyRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new MyResponse { Result = request.Number * 2 });
    }
}
```

For handlers with no input request:

```csharp
public class WeatherForecastHandler : RequestHandler<List<WeatherForecast>>
{
    public override Task<List<WeatherForecast>> HandleAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(GetForecasts());
    }
}
```

#### Mediator Pattern

Use the `ISender` interface to decouple request invocation:

```csharp
public async Task<IActionResult> GetForecast(string city, ISender sender)
{
    var forecast = await sender.SendAsync<string, WeatherForecast>(city);
    return Ok(forecast);
}
```

##### Request Behaviors

Behaviors are middleware for requests:

```csharp
public class LoggingBehaviour<TRequest, TResponse> : IPipelineBehaviour<TRequest, TResponse>
{
    private readonly ILogger<LoggingBehaviour<TRequest, TResponse>> _logger;

    public LoggingBehaviour(ILogger<LoggingBehaviour<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken, RequestHandlerDelegate<TResponse> next)
    {
        _logger.LogInformation("Handling {RequestType}", typeof(TRequest).Name);
        var response = await next();
        _logger.LogInformation("Handled {RequestType}", typeof(TRequest).Name);
        return response;
    }
}
```

##### Request Hooks

The library now supports three different types of hooks to provide more flexibility in your request processing pipeline:

```csharp
// Complete hook (both pre and post execution)
public class MyRequestHook : IRequestHook<MyRequest, MyResponse>
{
    public Task OnExecutingAsync(MyRequest request, CancellationToken cancellationToken)
    {
        Console.WriteLine("Before handling request");
        return Task.CompletedTask;
    }

    public Task OnExecutedAsync(MyRequest request, MyResponse response, CancellationToken cancellationToken)
    {
        Console.WriteLine("After handling request");
        return Task.CompletedTask;
    }
}

// Pre-execution only hook
public class MyRequestPreHook : IRequestPreHook<MyRequest>
{
    public Task OnExecutingAsync(MyRequest request, CancellationToken cancellationToken)
    {
        Console.WriteLine("Pre-hook executing");
        // You can modify request properties here
        return Task.CompletedTask;
    }
}

// Post-execution only hook
public class MyRequestPostHook : IRequestPostHook<MyRequest, MyResponse>
{
    public Task OnExecutedAsync(MyRequest request, MyResponse response, CancellationToken cancellationToken)
    {
        Console.WriteLine("Post-hook executed");
        // You can work with both request and response here
        return Task.CompletedTask;
    }
}
```

#### Registration and Configuration

```csharp
services.AddEasyRequestHandlers(typeof(Program))
        .WithMediatorPattern()
        .WithBehaviours(typeof(LoggingBehaviour<,>), typeof(ValidationBehaviour<,>))
        .WithRequestHooks()
        .Build();
```

Minimal API example:

```csharp
//Using direct handler injection.
app.MapGet("/weather-forecast", async (WeatherRequest request, WeatherForecastHandler handler) =>
{
    return await handler.HandleAsync(request);
});

//Using Mediator Pattern
app.MapGet("/weather-forecast", async (WeatherRequest request, ISender sender) =>
{
    return await sender.SendAsync<WeatherRequest, WeatherForecast?>(request, cancellationToken: cancellationToken);
});
```

---

### 📣 Event Handling
A single event can have multiple handlers. All handlers registered for an event will be invoked, and they can run either sequentially or in parallel depending on the event publisher's implementation.

#### Basic Event Handler

```csharp
public class MyEvent
{
    public string Message { get; set; }
}

public class MyEventHandler : IEventHandler<MyEvent>
{
    public Task HandleAsync(MyEvent @event, CancellationToken cancellationToken)
    {
        Console.WriteLine(@event.Message);
        return Task.CompletedTask;
    }
}
```

#### Publishing Events

```csharp
public class MyController
{
    private readonly IEventPublisher _publisher;

    public MyController(IEventPublisher publisher)
    {
        _publisher = publisher;
    }

    public async Task SendNotification(string message)
    {
        await _publisher.PublishAsync(new MyEvent { Message = message });
    }
}
```

---

## ✅ Summary

EasyRequestHandler provides a clean, extensible way to manage requests and events in .NET, with support for modern patterns like mediator, behaviors, and hooks—all without unnecessary boilerplate.

---

Licensed under MIT.
