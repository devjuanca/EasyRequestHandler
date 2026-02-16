# Easy Request Handler

**EasyRequestHandler** is a lightweight and extensible .NET library that simplifies request and event handling using patterns like Mediator and Event Dispatcher. It integrates seamlessly with the .NET Dependency Injection (DI) system and supports asynchronous operations, making it ideal for modular applications and event-driven systems.

## ✨ Features

### Core Capabilities

- **🎯 Mediator Pattern**: Centralizes request handling with the `ISender` interface, promoting loose coupling and separation of concerns
- **🔄 Flexible Request Handling**: Support for both input-based (`RequestHandler<TRequest, TResponse>`) and no-input handlers (`RequestHandler<TResponse>`)
- **📡 Event Dispatching**: Publish events to multiple handlers with support for both sequential and parallel execution
- **🔌 Automatic Registration**: Register all handlers with a single fluent API call using `IServiceCollection` extensions
- **🪝 Request Hooks**: Execute logic before and/or after request handling with three types of hooks:
  - `IRequestHook<TRequest, TResponse>` - Pre and post execution
  - `IRequestPreHook<TRequest>` - Pre-execution only
  - `IRequestPostHook<TRequest, TResponse>` - Post-execution only
- **🔧 Pipeline Behaviors**: Add cross-cutting concerns (logging, validation, caching, etc.) as middleware-style behaviors
- **📝 Built-in Logging**: Optional structured logging with contextual information for debugging and monitoring
- **⚡ Performance Optimized**: Singleton pattern for no-input requests, optimized task handling, and minimal allocations
- **🔒 Type-Safe**: Fully generic, compile-time type checking for requests, responses, and events
- **🧪 Testable**: Clean abstractions make unit testing straightforward

### Key Benefits

- **✅ Minimal Boilerplate**: Define handlers as simple classes—no complex setup required
- **✅ DI-First Design**: Built on Microsoft.Extensions.DependencyInjection for seamless integration
- **✅ Async by Default**: All operations use Task-based async patterns for scalability
- **✅ Zero Breaking Changes**: Backward compatible design maintains existing functionality
- **✅ Production Ready**: Comprehensive error handling, cancellation support, and security validated

## 📦 Installation

Install from NuGet using the .NET CLI:

```bash
dotnet add package EasyRequestHandler
```

Or using Package Manager Console:

```powershell
Install-Package EasyRequestHandler
```

## 🎯 Why Choose EasyRequestHandler?

### Compared to MediatR
- **Simpler API**: No need for `IRequest<T>` marker interfaces on your request types
- **Built-in Hooks**: Pre/post execution hooks without additional packages
- **Event Publishing**: Native support for event dispatching with parallel execution
- **Lightweight**: Minimal dependencies and smaller footprint

### Compared to Custom Solutions
- **Battle-Tested**: Proven patterns and comprehensive error handling
- **Extensible**: Easy to add behaviors, hooks, and custom logic
- **Maintained**: Regular updates and security fixes
- **Well-Documented**: Clear examples and API documentation

## 🚀 Quick Start

Here's a minimal example to get started in 3 steps:

### 1. Define Your Request and Handler

```csharp
// Your request
public class CalculateRequest
{
    public int X { get; set; }
    public int Y { get; set; }
}

// Your response
public class CalculateResponse
{
    public int Sum { get; set; }
}

// Your handler
public class CalculateHandler : RequestHandler<CalculateRequest, CalculateResponse>
{
    public override Task<CalculateResponse> HandleAsync(CalculateRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new CalculateResponse { Sum = request.X + request.Y });
    }
}
```

### 2. Register in DI Container

```csharp
// In Program.cs or Startup.cs
services.AddEasyRequestHandlers(typeof(Program))
        .WithMediatorPattern()  // Optional: enable ISender
        .Build();
```

### 3. Use in Your Application

```csharp
public class CalculatorController : ControllerBase
{
    private readonly ISender _sender;

    public CalculatorController(ISender sender) => _sender = sender;

    [HttpGet("calculate")]
    public async Task<IActionResult> Calculate(int x, int y)
    {
        var result = await _sender.SendAsync<CalculateRequest, CalculateResponse>(
            new CalculateRequest { X = x, Y = y });
        return Ok(result);
    }
}
```

That's it! Your handler is automatically discovered, registered, and ready to use.

---

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

### Perfect For

- ✅ **CQRS Applications**: Separate command and query handling with clear boundaries
- ✅ **Clean Architecture**: Enforce separation of concerns and dependency inversion
- ✅ **Event-Driven Systems**: Publish domain events and handle them asynchronously
- ✅ **Microservices**: Standardize request/event handling across services
- ✅ **API Development**: Build maintainable REST APIs with consistent patterns

### Getting Help

- 📖 [Documentation](https://github.com/devjuanca/EasyRequestHandler) - Full API reference and examples
- 🐛 [Issues](https://github.com/devjuanca/EasyRequestHandler/issues) - Report bugs or request features
- 💬 [Discussions](https://github.com/devjuanca/EasyRequestHandler/discussions) - Ask questions and share ideas

### Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

---

## 📄 License

Licensed under [MIT License](LICENSE.txt).

---

**Made with ❤️ by Juan Carlos Torres Cuervo**
