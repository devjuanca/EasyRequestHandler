using Microsoft.Extensions.DependencyInjection;
using EasyRequestHandlers.Request;
using Xunit;

namespace EasyRequestHandlers.Tests.RequestHandlers;

public class VoidHandlerTests
{
    public class CreateCommand { public string Name { get; set; } = string.Empty; }

    public class TrackingVoidHandler : VoidRequestHandler<CreateCommand>
    {
        public static bool WasExecuted { get; set; }
        public static string? LastName { get; set; }

        public override Task HandleAsync(CreateCommand request, CancellationToken cancellationToken = default)
        {
            WasExecuted = true;
            LastName = request.Name;
            return Task.CompletedTask;
        }
    }

    public class ThrowingCommand { public int Value { get; set; } }

    public class ThrowingVoidHandler : VoidRequestHandler<ThrowingCommand>
    {
        public override Task HandleAsync(ThrowingCommand request, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Void handler error");
        }
    }

    [Fact]
    public async Task Sender_SendAsync_VoidCommand_ExecutesHandler()
    {
        // Arrange
        TrackingVoidHandler.WasExecuted = false;
        TrackingVoidHandler.LastName = null;

        var services = new ServiceCollection();
        services.AddEasyRequestHandlers(typeof(VoidHandlerTests))
                .WithMediatorPattern()
                .Build();

        var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        // Act
        await sender.SendAsync(new CreateCommand { Name = "Test" });

        // Assert
        Assert.True(TrackingVoidHandler.WasExecuted);
        Assert.Equal("Test", TrackingVoidHandler.LastName);
    }

    [Fact]
    public async Task Sender_SendAsync_VoidCommand_NullRequest_ThrowsArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddEasyRequestHandlers(typeof(VoidHandlerTests))
                .WithMediatorPattern()
                .Build();

        var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => sender.SendAsync<CreateCommand>(null!));
    }

    [Fact]
    public async Task Sender_SendAsync_VoidCommand_PropagatesException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddEasyRequestHandlers(typeof(VoidHandlerTests))
                .WithMediatorPattern()
                .Build();

        var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sender.SendAsync(new ThrowingCommand { Value = 1 }));

        Assert.Equal("Void handler error", exception.Message);
    }

    [Fact]
    public void VoidHandler_IsRegistered_WithDirectInjection()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddEasyRequestHandlers(typeof(VoidHandlerTests))
                .Build();
        var provider = services.BuildServiceProvider();

        // Assert
        var handler = provider.GetService<TrackingVoidHandler>();
        Assert.NotNull(handler);
    }

    [Fact]
    public void VoidHandler_IsRegistered_WithMediatorPattern()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddEasyRequestHandlers(typeof(VoidHandlerTests))
                .WithMediatorPattern()
                .Build();
        var provider = services.BuildServiceProvider();

        // Assert - should be resolvable via base type
        var handler = provider.GetService<VoidRequestHandler<CreateCommand>>();
        Assert.NotNull(handler);
    }
}
