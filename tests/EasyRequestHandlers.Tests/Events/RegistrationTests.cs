using Microsoft.Extensions.DependencyInjection;
using EasyRequestHandlers.Events;
using EasyRequestHandlers.Common;
using Xunit;

namespace EasyRequestHandlers.Tests.Events;

public class RegistrationTests
{
    // Test event and handlers
    public class TestEvent
    {
        public string Message { get; set; } = string.Empty;
    }

    public class TestEventHandler : IEventHandler<TestEvent>
    {
        public Task HandleAsync(TestEvent @event, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    public class AnotherTestEventHandler : IEventHandler<TestEvent>
    {
        public Task HandleAsync(TestEvent @event, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    [Fact]
    public void AddEasyEventHandlers_RegistersHandlers()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddEasyEventHandlers(typeof(RegistrationTests));
        var provider = services.BuildServiceProvider();

        // Assert
        var handlers = provider.GetServices<IEventHandler<TestEvent>>();
        Assert.Equal(2, handlers.Count());
    }

    [Fact]
    public void AddEasyEventHandlers_RegistersEventPublisher()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddEasyEventHandlers(typeof(RegistrationTests));

        services.AddLogging();

        var provider = services.BuildServiceProvider();

        // Assert
        var publisher = provider.GetService<IEventPublisher>();
        Assert.NotNull(publisher);
    }

    [Fact]
    public void AddEasyEventHandlers_CalledTwice_DoesNotDuplicatePublisher()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddEasyEventHandlers(typeof(RegistrationTests));
        services.AddEasyEventHandlers(typeof(RegistrationTests));
        services.AddLogging();
        var provider = services.BuildServiceProvider();

        // Assert - TryAddSingleton prevents duplicate publisher
        var publishers = provider.GetServices<IEventPublisher>();
        Assert.Single(publishers);
    }

    // --- HandlerLifetimeAttribute ---

    public class LifetimeEvent { }

    [HandlerLifetime(ServiceLifetime.Singleton)]
    public class SingletonEventHandler : IEventHandler<LifetimeEvent>
    {
        public Task HandleAsync(LifetimeEvent @event, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    public class TransientEvent { }

    public class DefaultLifetimeEventHandler : IEventHandler<TransientEvent>
    {
        public Task HandleAsync(TransientEvent @event, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    [Fact]
    public void AddEasyEventHandlers_SingletonAttribute_RegistersAsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddEasyEventHandlers(typeof(RegistrationTests));
        var provider = services.BuildServiceProvider();

        // Assert - singleton returns the same instance
        var handler1 = provider.GetService<IEventHandler<LifetimeEvent>>();
        var handler2 = provider.GetService<IEventHandler<LifetimeEvent>>();
        Assert.NotNull(handler1);
        Assert.Same(handler1, handler2);
    }

    [Fact]
    public void AddEasyEventHandlers_DefaultLifetime_RegistersAsTransient()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddEasyEventHandlers(typeof(RegistrationTests));
        var provider = services.BuildServiceProvider();

        // Assert - transient returns different instances
        var handler1 = provider.GetService<IEventHandler<TransientEvent>>();
        var handler2 = provider.GetService<IEventHandler<TransientEvent>>();
        Assert.NotNull(handler1);
        Assert.NotSame(handler1, handler2);
    }
}
