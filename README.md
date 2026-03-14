# Easy Request Handler

**EasyRequestHandler** is a lightweight .NET library for request and event handling. It implements the Mediator pattern and an Event Dispatcher on top of Microsoft's built-in Dependency Injection, so you get clean separation of concerns with zero external dependencies beyond `Microsoft.Extensions.DependencyInjection`.

**Target:** .NET Standard 2.1 | **License:** MIT

## Installation

```bash
dotnet add package EasyRequestHandler
```

Or via Package Manager Console:

```powershell
Install-Package EasyRequestHandler
```

---

## Quick Start

### 1. Define a request, response, and handler

```csharp
public class CalculateRequest
{
    public int X { get; set; }
    public int Y { get; set; }
}

public class CalculateResponse
{
    public int Sum { get; set; }
}

public class CalculateHandler : RequestHandler<CalculateRequest, CalculateResponse>
{
    public override Task<CalculateResponse> HandleAsync(
        CalculateRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new CalculateResponse { Sum = request.X + request.Y });
    }
}
```

### 2. Register in the DI container

```csharp
services.AddEasyRequestHandlers(typeof(Program))
        .WithMediatorPattern()
        .Build();
```

### 3. Use it

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

That's it. The handler is automatically discovered, registered, and ready to use.

---

## Request Handling

### Two ways to invoke a handler

**Direct Injection** — inject the handler class and call `HandleAsync()`:

```csharp
app.MapGet("/forecast", async (WeatherForecastHandler handler, CancellationToken ct) =>
{
    return await handler.HandleAsync(ct);
});
```

**Mediator Pattern** — inject `ISender` for loose coupling:

```csharp
app.MapGet("/forecast/{city}", async (string city, ISender sender, CancellationToken ct) =>
{
    return await sender.SendAsync<string, WeatherForecast?>(city, cancellationToken: ct);
});
```

### Request handler with input

```csharp
public class MyRequestHandler : RequestHandler<MyRequest, MyResponse>
{
    public override Task<MyResponse> HandleAsync(
        MyRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new MyResponse { Result = request.Number * 2 });
    }
}
```

### Request handler without input

When a handler needs no input, extend `RequestHandler<TResponse>`:

```csharp
public class AllForecastsHandler : RequestHandler<List<WeatherForecast>>
{
    public override Task<List<WeatherForecast>> HandleAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(GetForecasts());
    }
}
```

Invoke it through `ISender`:

```csharp
var forecasts = await sender.SendAsync<List<WeatherForecast>>();
```

### The `Empty` response type

When a handler performs an action but has no meaningful return value, use `Empty` as the response type instead of inventing a dummy class:

```csharp
public class CreateForecastHandler : RequestHandler<CreateForecastCommand, Empty>
{
    public override Task<Empty> HandleAsync(
        CreateForecastCommand request, CancellationToken cancellationToken = default)
    {
        // ... create the forecast
        return Task.FromResult(Empty.Value);
    }
}
```

---

## Pipeline Behaviors

Behaviors wrap handler execution like middleware. They run in the order they are registered, forming a pipeline around the handler:

```
Request --> Behavior 1 --> Behavior 2 --> Handler --> Behavior 2 --> Behavior 1 --> Response
```

### Defining a behavior

Implement `IPipelineBehavior<TRequest, TResponse>`. Call `next()` to pass control to the next behavior (or the handler if there are no more behaviors):

```csharp
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request, CancellationToken cancellationToken, RequestHandlerDelegate<TResponse> next)
    {
        _logger.LogInformation("Handling {RequestType}", typeof(TRequest).Name);
        var response = await next();
        _logger.LogInformation("Handled {RequestType}", typeof(TRequest).Name);
        return response;
    }
}
```

### Registering behaviors

Register one or more behaviors in the builder. **Order matters** — behaviors execute in registration order:

```csharp
services.AddEasyRequestHandlers(typeof(Program))
        .WithMediatorPattern()
        .WithBehaviors(
            typeof(LoggingBehavior<,>),
            typeof(AuthenticationBehavior<,>),
            typeof(ValidationBehavior<,>)
        )
        .Build();
```

You can also register a single behavior with `.WithBehavior(typeof(LoggingBehavior<,>))`.

> **Note:** Behaviors require the mediator pattern. Calling `.WithBehaviors()` before `.WithMediatorPattern()` throws an `InvalidOperationException`.

---

## Request Hooks

Hooks let you run logic before and/or after a handler executes. Unlike behaviors, hooks don't wrap the handler — they run at fixed points in the pipeline. All hooks in the assembly are auto-discovered when enabled.

### Three hook types

**Full hook** — runs both before and after the handler:

```csharp
public class AuditHook : IRequestHook<MyRequest, MyResponse>
{
    public Task OnExecutingAsync(MyRequest request, CancellationToken cancellationToken)
    {
        // Runs before the handler
        return Task.CompletedTask;
    }

    public Task OnExecutedAsync(MyRequest request, MyResponse response, CancellationToken cancellationToken)
    {
        // Runs after the handler
        return Task.CompletedTask;
    }
}
```

**Pre-hook only:**

```csharp
public class ValidationPreHook : IRequestPreHook<MyRequest>
{
    public Task OnExecutingAsync(MyRequest request, CancellationToken cancellationToken)
    {
        // Validate or enrich the request before handling
        return Task.CompletedTask;
    }
}
```

**Post-hook only:**

```csharp
public class NotificationPostHook : IRequestPostHook<MyRequest, MyResponse>
{
    public Task OnExecutedAsync(MyRequest request, MyResponse response, CancellationToken cancellationToken)
    {
        // React to the result after handling
        return Task.CompletedTask;
    }
}
```

### Enabling hooks

```csharp
services.AddEasyRequestHandlers(typeof(Program))
        .WithMediatorPattern()
        .WithRequestHooks()
        .Build();
```

Hooks are auto-discovered from the scanned assemblies. No manual registration needed.

### Execution order

When both behaviors and hooks are enabled, the full pipeline is:

```
Pre-hooks --> Behaviors --> Handler --> Post-hooks
```

---

## Event Handling

The event system allows you to publish an event and have it handled by multiple independent handlers — useful for notifications, audit logging, cache invalidation, and other side effects.

### Defining events and handlers

An event is any class. Handlers implement `IEventHandler<TEvent>`:

```csharp
public class OrderPlacedEvent
{
    public Guid OrderId { get; set; }
    public string CustomerEmail { get; set; }
}

public class SendConfirmationEmail : IEventHandler<OrderPlacedEvent>
{
    public async Task HandleAsync(OrderPlacedEvent @event, CancellationToken cancellationToken)
    {
        // Send email...
    }
}

public class UpdateInventory : IEventHandler<OrderPlacedEvent>
{
    public async Task HandleAsync(OrderPlacedEvent @event, CancellationToken cancellationToken)
    {
        // Adjust stock levels...
    }
}
```

### Registering event handlers

```csharp
services.AddEasyEventHandlers(typeof(Program));
```

All `IEventHandler<>` implementations in the assembly are auto-discovered and registered.

### Publishing events

Inject `IEventPublisher` and call `PublishAsync`:

```csharp
public class OrderService
{
    private readonly IEventPublisher _publisher;

    public OrderService(IEventPublisher publisher) => _publisher = publisher;

    public async Task PlaceOrder(Order order)
    {
        // ... save the order

        await _publisher.PublishAsync(new OrderPlacedEvent
        {
            OrderId = order.Id,
            CustomerEmail = order.Email
        });
    }
}
```

### Execution modes

`PublishAsync` has two overloads: a simple one with defaults, and one that accepts `EventPublishOptions` for full control:

```csharp
// Simple — sequential, awaited (default behavior)
await publisher.PublishAsync(myEvent);

// With options — use EventPublishOptions to configure execution
await publisher.PublishAsync(myEvent, new EventPublishOptions
{
    UseParallelExecution = true
});

// Fire-and-forget — dispatches to background, returns immediately
await publisher.PublishAsync(myEvent, new EventPublishOptions
{
    FireAndForget = true
});

// Fire-and-forget with timeout — cancels background handlers if they exceed the limit
await publisher.PublishAsync(myEvent, new EventPublishOptions
{
    FireAndForget = true,
    FireAndForgetTimeout = TimeSpan.FromSeconds(30)
});

// Stop on error — if a handler fails, subsequent handlers are skipped (sequential only)
await publisher.PublishAsync(myEvent, new EventPublishOptions
{
    StopOnError = true
});
```

#### `EventPublishOptions`

| Property | Default | Behavior |
|----------|---------|----------|
| `UseParallelExecution` | `false` | `false`: handlers run one after another. `true`: all handlers start concurrently. |
| `FireAndForget` | `false` | `true`: returns immediately, handlers run in the background. `false`: awaits all handlers before returning. |
| `StopOnError` | `false` | `true`: stops sequential execution at the first handler failure. Ignored when `UseParallelExecution` is `true`. |
| `FireAndForgetTimeout` | `null` | When set, cancels background handlers that exceed the specified duration. Only applies when `FireAndForget` is `true`. |

### Error resilience

By default, a failing handler does **not** stop the others. In both sequential and parallel modes, all handlers get a chance to execute. Each error is logged individually. When `fireAndForget` is `false`, an `AggregateException` containing all handler errors is thrown after all handlers complete.

When `stopOnError` is `true` and handlers run sequentially, the pipeline halts at the first failure — remaining handlers are skipped. This is useful for ordered workflows where later steps depend on earlier ones (e.g., `UpdateOrder` → `SendEmail` → `SendWhatsApp` — if the order update fails, notifications should not be sent). The error from the failed handler is still thrown as an `AggregateException`.

### Fire-and-forget safety

When `fireAndForget` is `true`, the caller's `CancellationToken` is intentionally **not** forwarded to the background handlers. In ASP.NET Core, the request token is cancelled as soon as the response is sent — since fire-and-forget returns immediately, forwarding it would cancel the background work almost instantly.

To prevent background handlers from running indefinitely, use `FireAndForgetTimeout` to set a maximum duration:

```csharp
// Handlers have up to 30 seconds to complete; cancelled after that
await publisher.PublishAsync(orderEvent, new EventPublishOptions
{
    FireAndForget = true,
    FireAndForgetTimeout = TimeSpan.FromSeconds(30)
});
```

If the timeout is exceeded, a warning is logged and the remaining work is cancelled. When no timeout is specified (`null`), handlers run without a time limit.

### Handler ordering

By default, handlers execute in registration order. Use `[HandlerOrder]` to control priority — lower values run first:

```csharp
[HandlerOrder(1)]
public class AuditHandler : IEventHandler<OrderPlacedEvent>
{
    public Task HandleAsync(OrderPlacedEvent @event, CancellationToken cancellationToken)
    {
        // Runs first
        return Task.CompletedTask;
    }
}

[HandlerOrder(2)]
public class NotificationHandler : IEventHandler<OrderPlacedEvent>
{
    public Task HandleAsync(OrderPlacedEvent @event, CancellationToken cancellationToken)
    {
        // Runs second
        return Task.CompletedTask;
    }
}
```

Handlers without `[HandlerOrder]` run after all ordered handlers, preserving their registration order.

### Handler lifetime

Event handlers default to **Transient**. Use `[HandlerLifetime]` to change it:

```csharp
[HandlerLifetime(ServiceLifetime.Singleton)]
public class CacheInvalidationHandler : IEventHandler<ProductUpdatedEvent>
{
    public Task HandleAsync(ProductUpdatedEvent @event, CancellationToken cancellationToken)
    {
        // This instance is reused across all event dispatches
        return Task.CompletedTask;
    }
}
```

> **Note:** Event handlers always execute in a fresh DI scope created by the publisher, regardless of their registered lifetime. This means scoped services injected into handlers are independent from the caller's scope.

---

## Full Registration Example

```csharp
var builder = WebApplication.CreateBuilder(args);

// Request handlers with mediator, behaviors, and hooks
builder.Services.AddEasyRequestHandlers(typeof(Program))
    .WithMediatorPattern()
    .WithBehaviors(
        typeof(LoggingBehavior<,>),
        typeof(AuthenticationBehavior<,>),
        typeof(ValidationBehavior<,>)
    )
    .WithRequestHooks()
    .Build();

// Event handlers (auto-discovered)
builder.Services.AddEasyEventHandlers(typeof(Program));

var app = builder.Build();

// Direct handler injection
app.MapGet("/forecasts", async (AllForecastsHandler handler, CancellationToken ct) =>
{
    return await handler.HandleAsync(ct);
});

// Mediator pattern
app.MapGet("/forecast/{city}", async (string city, ISender sender, CancellationToken ct) =>
{
    var result = await sender.SendAsync<string, WeatherForecast?>(city, cancellationToken: ct);
    return result is not null ? Results.Ok(result) : Results.NotFound();
});

// Event publishing
app.MapPost("/notification", async (NotificationEvent @event, IEventPublisher publisher) =>
{
    await publisher.PublishAsync(@event, new EventPublishOptions { UseParallelExecution = true });
    return Results.NoContent();
});

app.Run();
```

---

## Why EasyRequestHandler?

### vs MediatR
- **No marker interfaces** — your request types stay clean (no `IRequest<T>`)
- **Built-in hooks** — pre/post execution without additional packages
- **Event publisher included** — fire-and-forget, parallel execution, handler ordering, error resilience, and stop-on-error for sequential workflows
- **Smaller footprint** — fewer dependencies, less ceremony

### vs hand-rolled solutions
- **Auto-discovery** — handlers are found by assembly scanning, no manual wiring
- **Pipeline behaviors** — cross-cutting concerns without repetition
- **Tested patterns** — mediator, event dispatcher, and DI integration out of the box

---

## Use Cases

- **CQRS** — separate command and query handling with clear boundaries
- **Clean Architecture** — enforce separation of concerns with handler-per-feature
- **Event-Driven Systems** — publish domain events and react asynchronously
- **Microservices** — standardize request/event handling across services
- **API Development** — build maintainable APIs with consistent patterns

---

## Getting Help

- [Documentation & Source](https://github.com/devjuanca/EasyRequestHandler)
- [Issues](https://github.com/devjuanca/EasyRequestHandler/issues) — report bugs or request features
- [Discussions](https://github.com/devjuanca/EasyRequestHandler/discussions) — questions and ideas

Contributions are welcome! Feel free to submit a Pull Request.

---

## License

Licensed under [MIT License](LICENSE.txt).

**Made with care by Juan Carlos Torres Cuervo**
