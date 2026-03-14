using Microsoft.Extensions.DependencyInjection;
using EasyRequestHandlers.Request;
using Xunit;

namespace EasyRequestHandlers.Tests.RequestHandlers;

public class HooksTests
{
    // Test response and handlers
    public class TestResponse { public int Result { get; set; } }

    // No-input handler
    public class NoInputTestHandler : RequestHandler<TestResponse>
    {
        public override Task<TestResponse> HandleAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TestResponse { Result = 42 });
        }
    }

    // Hook for no-input request
    public class NoInputRequestHook : IRequestHook<EmptyRequest, TestResponse>
    {
        public static bool PreExecuted { get; set; }
        public static bool PostExecuted { get; set; }

        public Task OnExecutingAsync(EmptyRequest request, CancellationToken cancellationToken = default)
        {
            PreExecuted = true;
            return Task.CompletedTask;
        }

        public Task OnExecutedAsync(EmptyRequest request, TestResponse response, CancellationToken cancellationToken = default)
        {
            PostExecuted = true;
            return Task.CompletedTask;
        }
    }

    // Behavior for no-input request
    public class NoInputBehavior : IPipelineBehavior<EmptyRequest, TestResponse>
    {
        public static bool BehaviorCalled { get; set; }

        public Task<TestResponse> Handle(EmptyRequest request, CancellationToken cancellationToken, RequestHandlerDelegate<TestResponse> next)
        {
            BehaviorCalled = true;
            return next();
        }
    }

    [Fact]
    public async Task Hooks_WorkWithNoInputHandlers()
    {
        // Arrange
        NoInputRequestHook.PreExecuted = false;
        NoInputRequestHook.PostExecuted = false;

        var services = new ServiceCollection();

        services.AddEasyRequestHandlers(typeof(HooksTests))
                .WithMediatorPattern()
                .WithRequestHooks()
                .Build();

        services.AddTransient<IRequestHook<EmptyRequest, TestResponse>, NoInputRequestHook>();

        var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        // Act
        var result = await sender.SendAsync<TestResponse>();

        // Assert
        Assert.Equal(42, result.Result);
        Assert.True(NoInputRequestHook.PreExecuted, "Pre-execution hook should have run");
        Assert.True(NoInputRequestHook.PostExecuted, "Post-execution hook should have run");
    }

    [Fact]
    public async Task NoInput_Handler_CanBeExecuted()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddEasyRequestHandlers(typeof(HooksTests))
                .WithMediatorPattern()
                .Build();

        var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        // Act
        var result = await sender.SendAsync<TestResponse>();

        // Assert
        Assert.Equal(42, result.Result);
    }

    // --- Full IRequestHook auto-discovery ---

    public class HookRequest { public int Value { get; set; } }
    public class HookResponse { public int Result { get; set; } }

    public class HookRequestHandler : RequestHandler<HookRequest, HookResponse>
    {
        public override Task<HookResponse> HandleAsync(HookRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new HookResponse { Result = request.Value });
        }
    }

    public class AutoDiscoveredHook : IRequestHook<HookRequest, HookResponse>
    {
        public static bool PreExecuted { get; set; }
        public static bool PostExecuted { get; set; }

        public Task OnExecutingAsync(HookRequest request, CancellationToken cancellationToken = default)
        {
            PreExecuted = true;
            return Task.CompletedTask;
        }

        public Task OnExecutedAsync(HookRequest request, HookResponse response, CancellationToken cancellationToken = default)
        {
            PostExecuted = true;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Hook_AutoDiscoveredFromAssembly_ExecutesPreAndPost()
    {
        // Arrange
        AutoDiscoveredHook.PreExecuted = false;
        AutoDiscoveredHook.PostExecuted = false;

        var services = new ServiceCollection();
        services.AddEasyRequestHandlers(typeof(HooksTests))
                .WithMediatorPattern()
                .WithRequestHooks()
                .Build();

        var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        // Act
        var result = await sender.SendAsync<HookRequest, HookResponse>(new HookRequest { Value = 10 });

        // Assert
        Assert.Equal(10, result.Result);
        Assert.True(AutoDiscoveredHook.PreExecuted, "Auto-discovered hook pre-phase should have run");
        Assert.True(AutoDiscoveredHook.PostExecuted, "Auto-discovered hook post-phase should have run");
    }

    // --- IRequestPreHook auto-discovery ---

    public class PreHookOnlyRequest { public int Value { get; set; } }
    public class PreHookOnlyResponse { public int Result { get; set; } }

    public class PreHookOnlyHandler : RequestHandler<PreHookOnlyRequest, PreHookOnlyResponse>
    {
        public override Task<PreHookOnlyResponse> HandleAsync(PreHookOnlyRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PreHookOnlyResponse { Result = request.Value });
        }
    }

    public class AutoDiscoveredPreHook : IRequestPreHook<PreHookOnlyRequest>
    {
        public static bool Executed { get; set; }

        public Task OnExecutingAsync(PreHookOnlyRequest request, CancellationToken cancellationToken = default)
        {
            Executed = true;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task PreHook_AutoDiscoveredFromAssembly_Executes()
    {
        // Arrange
        AutoDiscoveredPreHook.Executed = false;

        var services = new ServiceCollection();
        services.AddEasyRequestHandlers(typeof(HooksTests))
                .WithMediatorPattern()
                .WithRequestHooks()
                .Build();

        var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        // Act
        await sender.SendAsync<PreHookOnlyRequest, PreHookOnlyResponse>(new PreHookOnlyRequest { Value = 5 });

        // Assert
        Assert.True(AutoDiscoveredPreHook.Executed, "Auto-discovered pre-hook should have run");
    }

    // --- Behavior execution order ---

    public class OrderRequest { public int Value { get; set; } }
    public class OrderResponse { public int Result { get; set; } }

    public class OrderHandler : RequestHandler<OrderRequest, OrderResponse>
    {
        public override Task<OrderResponse> HandleAsync(OrderRequest request, CancellationToken cancellationToken = default)
        {
            ExecutionTracker.Log.Add("Handler");
            return Task.FromResult(new OrderResponse { Result = request.Value });
        }
    }

    public static class ExecutionTracker
    {
        public static List<string> Log { get; } = new();
    }

    public class FirstBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    {
        public async Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken, RequestHandlerDelegate<TResponse> next)
        {
            ExecutionTracker.Log.Add("First-Before");
            var result = await next();
            ExecutionTracker.Log.Add("First-After");
            return result;
        }
    }

    public class SecondBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    {
        public async Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken, RequestHandlerDelegate<TResponse> next)
        {
            ExecutionTracker.Log.Add("Second-Before");
            var result = await next();
            ExecutionTracker.Log.Add("Second-After");
            return result;
        }
    }

    [Fact]
    public async Task Behaviors_ExecuteInRegistrationOrder_WrappingHandler()
    {
        // Arrange
        ExecutionTracker.Log.Clear();

        var services = new ServiceCollection();
        services.AddEasyRequestHandlers(typeof(HooksTests))
                .WithMediatorPattern()
                .WithBehaviors(typeof(FirstBehavior<,>), typeof(SecondBehavior<,>))
                .Build();

        var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        // Act
        await sender.SendAsync<OrderRequest, OrderResponse>(new OrderRequest { Value = 1 });

        // Assert - pipeline wraps like middleware: First → Second → Handler → Second → First
        Assert.Equal(5, ExecutionTracker.Log.Count);
        Assert.Equal("First-Before", ExecutionTracker.Log[0]);
        Assert.Equal("Second-Before", ExecutionTracker.Log[1]);
        Assert.Equal("Handler", ExecutionTracker.Log[2]);
        Assert.Equal("Second-After", ExecutionTracker.Log[3]);
        Assert.Equal("First-After", ExecutionTracker.Log[4]);
    }

    // --- Hooks disabled: hooks should not run ---

    [Fact]
    public async Task HooksDisabled_HooksDoNotRun()
    {
        // Arrange
        AutoDiscoveredHook.PreExecuted = false;
        AutoDiscoveredHook.PostExecuted = false;

        var services = new ServiceCollection();
        services.AddEasyRequestHandlers(typeof(HooksTests))
                .WithMediatorPattern()
                // NOTE: .WithRequestHooks() is NOT called
                .Build();

        var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        // Act
        var result = await sender.SendAsync<HookRequest, HookResponse>(new HookRequest { Value = 10 });

        // Assert
        Assert.Equal(10, result.Result);
        Assert.False(AutoDiscoveredHook.PreExecuted, "Hooks should not run when not enabled");
        Assert.False(AutoDiscoveredHook.PostExecuted, "Hooks should not run when not enabled");
    }
}
