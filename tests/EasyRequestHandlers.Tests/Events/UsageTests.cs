using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using EasyRequestHandlers.Events;
using EasyRequestHandlers.Common;
using Xunit;

namespace EasyRequestHandlers.Tests.Events;

public class UsageTests
{
    // Test event and handlers with tracking
    public class TestEvent
    {
        public string Message { get; set; } = string.Empty;
    }

    public class TrackingEventHandler : IEventHandler<TestEvent>
    {
        public static bool WasHandled { get; set; }

        public Task HandleAsync(TestEvent @event, CancellationToken cancellationToken)
        {
            WasHandled = true;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task EventPublisher_InvokesHandlers()
    {
        // Arrange
        TrackingEventHandler.WasHandled = false;
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());

        services.AddEasyEventHandlers(typeof(UsageTests));

        services.AddSingleton<IEventHandler<TestEvent>, TrackingEventHandler>();

        var provider = services.BuildServiceProvider();

        var publisher = provider.GetRequiredService<IEventPublisher>();

        // Act
        await publisher.PublishAsync(new TestEvent { Message = "Test message" });

        // Assert
        Assert.True(TrackingEventHandler.WasHandled);
    }

    [Fact]
    public async Task EventPublisher_WithParallelExecution_InvokesHandlers()
    {
        // Arrange
        TrackingEventHandler.WasHandled = false;

        var services = new ServiceCollection();

        services.AddLogging(builder => builder.AddConsole());

        services.AddEasyEventHandlers(typeof(UsageTests));

        services.AddSingleton<IEventHandler<TestEvent>, TrackingEventHandler>();

        var provider = services.BuildServiceProvider();

        var publisher = provider.GetRequiredService<IEventPublisher>();

        // Act
        await publisher.PublishAsync(new TestEvent { Message = "Test message" }, new EventPublishOptions
        {
            UseParallelExecution = true
        });

        // Assert
        Assert.True(TrackingEventHandler.WasHandled);
    }

    [Fact]
    public async Task EventPublisher_NullEvent_ThrowsArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEasyEventHandlers(typeof(UsageTests));
        var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IEventPublisher>();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => publisher.PublishAsync<TestEvent>(null!));
    }

    [Fact]
    public async Task EventPublisher_NoHandlersRegistered_DoesNotThrow()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEasyEventHandlers(typeof(UsageTests));
        var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IEventPublisher>();

        // Use an event type with no registered handlers
        // Act & Assert - should complete without exception
        await publisher.PublishAsync(new UnhandledEvent());
    }

    public class UnhandledEvent { }

    // --- Sequential continue-on-error ---

    public class ErrorEvent
    {
        public List<string> ExecutionLog { get; } = new();
    }

    public class FailingHandler1 : IEventHandler<ErrorEvent>
    {
        public async Task HandleAsync(ErrorEvent @event, CancellationToken cancellationToken)
        {
            @event.ExecutionLog.Add("FailingHandler1");
            await Task.CompletedTask;
            throw new InvalidOperationException("Handler1 failed");
        }
    }

    public class SucceedingHandler : IEventHandler<ErrorEvent>
    {
        public Task HandleAsync(ErrorEvent @event, CancellationToken cancellationToken)
        {
            @event.ExecutionLog.Add("SucceedingHandler");
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task EventPublisher_Sequential_ContinuesOnError_AllHandlersExecute()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTransient<IEventHandler<ErrorEvent>, FailingHandler1>();
        services.AddTransient<IEventHandler<ErrorEvent>, SucceedingHandler>();
        services.AddEasyEventHandlers(typeof(UsageTests));
        var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IEventPublisher>();
        var errorEvent = new ErrorEvent();

        // Act
        var ex = await Assert.ThrowsAsync<AggregateException>(
            () => publisher.PublishAsync(errorEvent));

        // Assert - both handlers ran despite the first one throwing
        Assert.Contains("FailingHandler1", errorEvent.ExecutionLog);
        Assert.Contains("SucceedingHandler", errorEvent.ExecutionLog);
        Assert.Single(ex.InnerExceptions);
        Assert.IsType<InvalidOperationException>(ex.InnerExceptions[0]);
        Assert.Equal("Handler1 failed", ex.InnerExceptions[0].InnerException!.Message);
    }

    [Fact]
    public async Task EventPublisher_Parallel_ContinuesOnError_AllHandlersExecute()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTransient<IEventHandler<ErrorEvent>, FailingHandler1>();
        services.AddTransient<IEventHandler<ErrorEvent>, SucceedingHandler>();
        services.AddEasyEventHandlers(typeof(UsageTests));
        var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IEventPublisher>();
        var errorEvent = new ErrorEvent();

        // Act
        var ex = await Assert.ThrowsAsync<AggregateException>(
            () => publisher.PublishAsync(errorEvent, new EventPublishOptions { UseParallelExecution = true }));

        // Assert - both handlers ran
        Assert.Contains("FailingHandler1", errorEvent.ExecutionLog);
        Assert.Contains("SucceedingHandler", errorEvent.ExecutionLog);
        Assert.Single(ex.InnerExceptions);
    }

    // --- Fire-and-forget ---

    public class SlowEvent
    {
        public static bool HandlerCompleted { get; set; }
    }

    public class SlowEventHandler : IEventHandler<SlowEvent>
    {
        public async Task HandleAsync(SlowEvent @event, CancellationToken cancellationToken)
        {
            await Task.Delay(200, cancellationToken);
            SlowEvent.HandlerCompleted = true;
        }
    }

    [Fact]
    public async Task EventPublisher_FireAndForget_ReturnsImmediately()
    {
        // Arrange
        SlowEvent.HandlerCompleted = false;
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTransient<IEventHandler<SlowEvent>, SlowEventHandler>();
        services.AddEasyEventHandlers(typeof(UsageTests));
        var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IEventPublisher>();

        // Act - fire and forget
        await publisher.PublishAsync(new SlowEvent(), new EventPublishOptions { FireAndForget = true });

        // Assert - should return before handler completes
        Assert.False(SlowEvent.HandlerCompleted);

        // Wait for background work to finish
        await Task.Delay(500);
        Assert.True(SlowEvent.HandlerCompleted);
    }

    [Fact]
    public async Task EventPublisher_FireAndForget_ErrorsDoNotPropagate()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTransient<IEventHandler<ErrorEvent>, FailingHandler1>();
        services.AddEasyEventHandlers(typeof(UsageTests));
        var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IEventPublisher>();

        // Act & Assert - fire-and-forget should not throw even if handlers fail
        await publisher.PublishAsync(new ErrorEvent(), new EventPublishOptions { FireAndForget = true });

        // Give background task time to complete and log
        await Task.Delay(100);
    }

    [Fact]
    public async Task EventPublisher_FireAndForget_WithTimeout_CancelsLongRunningHandlers()
    {
        // Arrange
        SlowEvent.HandlerCompleted = false;
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTransient<IEventHandler<SlowEvent>, SlowEventHandler>();
        services.AddEasyEventHandlers(typeof(UsageTests));
        var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IEventPublisher>();

        // Act - fire and forget with a timeout shorter than the handler's delay
        await publisher.PublishAsync(new SlowEvent(), new EventPublishOptions
        {
            FireAndForget = true,
            FireAndForgetTimeout = TimeSpan.FromMilliseconds(50)
        });

        // Wait enough time for handler to have completed if it wasn't cancelled
        await Task.Delay(500);

        // Assert - handler should NOT have completed because it was cancelled by timeout
        Assert.False(SlowEvent.HandlerCompleted);
    }

    [Fact]
    public async Task EventPublisher_FireAndForget_WithTimeout_CompletesWhenWithinLimit()
    {
        // Arrange
        SlowEvent.HandlerCompleted = false;
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTransient<IEventHandler<SlowEvent>, SlowEventHandler>();
        services.AddEasyEventHandlers(typeof(UsageTests));
        var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IEventPublisher>();

        // Act - fire and forget with a timeout longer than the handler's delay
        await publisher.PublishAsync(new SlowEvent(), new EventPublishOptions
        {
            FireAndForget = true,
            FireAndForgetTimeout = TimeSpan.FromSeconds(5)
        });

        // Wait for handler to finish
        await Task.Delay(500);

        // Assert - handler should have completed within the timeout
        Assert.True(SlowEvent.HandlerCompleted);
    }

    // --- Handler ordering ---

    public class OrderEvent
    {
        public static List<string> ExecutionOrder { get; } = new();
    }

    [HandlerOrder(3)]
    public class ThirdHandler : IEventHandler<OrderEvent>
    {
        public Task HandleAsync(OrderEvent @event, CancellationToken cancellationToken)
        {
            OrderEvent.ExecutionOrder.Add("Third");
            return Task.CompletedTask;
        }
    }

    [HandlerOrder(1)]
    public class FirstHandler : IEventHandler<OrderEvent>
    {
        public Task HandleAsync(OrderEvent @event, CancellationToken cancellationToken)
        {
            OrderEvent.ExecutionOrder.Add("First");
            return Task.CompletedTask;
        }
    }

    [HandlerOrder(2)]
    public class SecondHandler : IEventHandler<OrderEvent>
    {
        public Task HandleAsync(OrderEvent @event, CancellationToken cancellationToken)
        {
            OrderEvent.ExecutionOrder.Add("Second");
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task EventPublisher_Sequential_RespectsHandlerOrder()
    {
        // Arrange
        OrderEvent.ExecutionOrder.Clear();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEasyEventHandlers(typeof(UsageTests));
        var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IEventPublisher>();

        // Act
        await publisher.PublishAsync(new OrderEvent());

        // Assert - should execute in attribute order: 1, 2, 3
        Assert.Equal(3, OrderEvent.ExecutionOrder.Count);
        Assert.Equal("First", OrderEvent.ExecutionOrder[0]);
        Assert.Equal("Second", OrderEvent.ExecutionOrder[1]);
        Assert.Equal("Third", OrderEvent.ExecutionOrder[2]);
    }

    // --- Unordered handlers preserve registration order ---

    public class UnorderedEvent
    {
        public static List<string> ExecutionOrder { get; } = new();
    }

    public class UnorderedHandlerA : IEventHandler<UnorderedEvent>
    {
        public Task HandleAsync(UnorderedEvent @event, CancellationToken cancellationToken)
        {
            UnorderedEvent.ExecutionOrder.Add("A");
            return Task.CompletedTask;
        }
    }

    public class UnorderedHandlerB : IEventHandler<UnorderedEvent>
    {
        public Task HandleAsync(UnorderedEvent @event, CancellationToken cancellationToken)
        {
            UnorderedEvent.ExecutionOrder.Add("B");
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task EventPublisher_HandlersWithoutOrderAttribute_PreserveRegistrationOrder()
    {
        // Arrange
        UnorderedEvent.ExecutionOrder.Clear();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTransient<IEventHandler<UnorderedEvent>, UnorderedHandlerA>();
        services.AddTransient<IEventHandler<UnorderedEvent>, UnorderedHandlerB>();
        services.AddEasyEventHandlers(typeof(UsageTests));
        var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IEventPublisher>();

        // Act
        await publisher.PublishAsync(new UnorderedEvent());

        // Assert - should preserve registration order: A, B
        Assert.Equal(2, UnorderedEvent.ExecutionOrder.Count);
        Assert.Equal("A", UnorderedEvent.ExecutionOrder[0]);
        Assert.Equal("B", UnorderedEvent.ExecutionOrder[1]);
    }

    // --- StopOnError ---

    [Fact]
    public async Task EventPublisher_Sequential_StopOnError_StopsAfterFirstFailure()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTransient<IEventHandler<ErrorEvent>, FailingHandler1>();
        services.AddTransient<IEventHandler<ErrorEvent>, SucceedingHandler>();
        services.AddEasyEventHandlers(typeof(UsageTests));
        var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IEventPublisher>();
        var errorEvent = new ErrorEvent();

        // Act
        var ex = await Assert.ThrowsAsync<AggregateException>(
            () => publisher.PublishAsync(errorEvent, new EventPublishOptions { StopOnError = true }));

        // Assert - only the failing handler ran, the succeeding one was skipped
        Assert.Single(errorEvent.ExecutionLog);
        Assert.Equal("FailingHandler1", errorEvent.ExecutionLog[0]);
        Assert.Single(ex.InnerExceptions);
    }

    [Fact]
    public async Task EventPublisher_Sequential_StopOnError_AllRunWhenNoErrors()
    {
        // Arrange
        UnorderedEvent.ExecutionOrder.Clear();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTransient<IEventHandler<UnorderedEvent>, UnorderedHandlerA>();
        services.AddTransient<IEventHandler<UnorderedEvent>, UnorderedHandlerB>();
        services.AddEasyEventHandlers(typeof(UsageTests));
        var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IEventPublisher>();

        // Act - stopOnError with no errors should execute all handlers
        await publisher.PublishAsync(new UnorderedEvent(), new EventPublishOptions { StopOnError = true });

        // Assert
        Assert.Equal(2, UnorderedEvent.ExecutionOrder.Count);
        Assert.Equal("A", UnorderedEvent.ExecutionOrder[0]);
        Assert.Equal("B", UnorderedEvent.ExecutionOrder[1]);
    }

    [Fact]
    public async Task EventPublisher_Parallel_StopOnError_IsIgnored_AllHandlersRun()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTransient<IEventHandler<ErrorEvent>, FailingHandler1>();
        services.AddTransient<IEventHandler<ErrorEvent>, SucceedingHandler>();
        services.AddEasyEventHandlers(typeof(UsageTests));
        var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IEventPublisher>();
        var errorEvent = new ErrorEvent();

        // Act - stopOnError is ignored in parallel mode, all handlers should run
        var ex = await Assert.ThrowsAsync<AggregateException>(
            () => publisher.PublishAsync(errorEvent, new EventPublishOptions
            {
                UseParallelExecution = true,
                StopOnError = true
            }));

        // Assert - both handlers ran because parallel ignores stopOnError
        Assert.Contains("FailingHandler1", errorEvent.ExecutionLog);
        Assert.Contains("SucceedingHandler", errorEvent.ExecutionLog);
        Assert.Single(ex.InnerExceptions);
    }
}
