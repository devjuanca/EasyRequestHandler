using Microsoft.Extensions.DependencyInjection;
using EasyRequestHandlers.Request;
using Xunit;
using Microsoft.Extensions.Logging;

namespace EasyRequestHandlers.Tests.RequestHandlers;

public class ErrorHandlingTests
{
    // Use different request types to avoid conflicts
    public class ErrorTestRequest { public int Value { get; set; } }
    public class ErrorTestResponse { public int Result { get; set; } }
    
    public class SuccessRequest { public int Value { get; set; } }
    public class SuccessResponse { public int Result { get; set; } }

    public class ThrowingHandler : RequestHandler<ErrorTestRequest, ErrorTestResponse>
    {
        public override Task<ErrorTestResponse> HandleAsync(ErrorTestRequest request, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Handler error");
        }
    }

    public class SuccessHandler : RequestHandler<SuccessRequest, SuccessResponse>
    {
        public override Task<SuccessResponse> HandleAsync(SuccessRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new SuccessResponse { Result = request.Value * 2 });
        }
    }

    public class PreHookRequest { public int Value { get; set; } }
    public class PreHookResponse { public int Result { get; set; } }
    
    public class PostHookRequest { public int Value { get; set; } }
    public class PostHookResponse { public int Result { get; set; } }

    public class PreHookHandler : RequestHandler<PreHookRequest, PreHookResponse>
    {
        public override Task<PreHookResponse> HandleAsync(PreHookRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PreHookResponse { Result = request.Value * 2 });
        }
    }

    public class PostHookHandler : RequestHandler<PostHookRequest, PostHookResponse>
    {
        public override Task<PostHookResponse> HandleAsync(PostHookRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PostHookResponse { Result = request.Value * 2 });
        }
    }

    public class ThrowingPreHook : IRequestPreHook<PreHookRequest>
    {
        public Task OnExecutingAsync(PreHookRequest request, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Pre-hook error");
        }
    }

    public class ThrowingPostHook : IRequestPostHook<PostHookRequest, PostHookResponse>
    {
        public Task OnExecutedAsync(PostHookRequest request, PostHookResponse response, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Post-hook error");
        }
    }

    public class ThrowingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    {
        public Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken, RequestHandlerDelegate<TResponse> next)
        {
            throw new InvalidOperationException("Behavior error");
        }
    }

    [Fact]
    public async Task Sender_NullRequest_ThrowsArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddEasyRequestHandlers(typeof(ErrorHandlingTests))
                .WithMediatorPattern()
                .Build();

        var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => sender.SendAsync<SuccessRequest, SuccessResponse>(null!));
    }

    [Fact]
    public async Task Sender_HandlerThrowsException_PropagatesException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddEasyRequestHandlers(typeof(ErrorHandlingTests))
                .WithMediatorPattern()
                .Build();

        var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sender.SendAsync<ErrorTestRequest, ErrorTestResponse>(new ErrorTestRequest { Value = 10 }));
        
        Assert.Equal("Handler error", exception.Message);
    }

    [Fact]
    public async Task Sender_PreHookThrows_PropagatesException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<IRequestPreHook<PreHookRequest>, ThrowingPreHook>();
        
        services.AddEasyRequestHandlers(typeof(ErrorHandlingTests))
                .WithMediatorPattern()
                .WithRequestHooks()
                .Build();

        var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sender.SendAsync<PreHookRequest, PreHookResponse>(new PreHookRequest { Value = 10 }));
        
        Assert.Equal("Pre-hook error", exception.Message);
    }

    [Fact]
    public async Task Sender_PostHookThrows_PropagatesException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<IRequestPostHook<PostHookRequest, PostHookResponse>, ThrowingPostHook>();
        
        services.AddEasyRequestHandlers(typeof(ErrorHandlingTests))
                .WithMediatorPattern()
                .WithRequestHooks()
                .Build();

        var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sender.SendAsync<PostHookRequest, PostHookResponse>(new PostHookRequest { Value = 10 }));
        
        Assert.Equal("Post-hook error", exception.Message);
    }

    [Fact]
    public async Task Sender_BehaviorThrows_PropagatesException()
    {
        // Arrange
        var services = new ServiceCollection();
        
        services.AddEasyRequestHandlers(typeof(ErrorHandlingTests))
                .WithMediatorPattern()
                .WithBehavior(typeof(ThrowingBehavior<,>))
                .Build();

        var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sender.SendAsync<ErrorTestRequest, ErrorTestResponse>(new ErrorTestRequest { Value = 10 }));
        
        Assert.Equal("Behavior error", exception.Message);
    }

    [Fact]
    public async Task Sender_WithLogging_DoesNotThrow()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        
        services.AddEasyRequestHandlers(typeof(ErrorHandlingTests))
                .WithMediatorPattern()
                .Build();

        var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        // Act
        var result = await sender.SendAsync<SuccessRequest, SuccessResponse>(new SuccessRequest { Value = 10 });

        // Assert
        Assert.Equal(20, result.Result);
    }
}
