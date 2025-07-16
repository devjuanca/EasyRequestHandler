using Microsoft.Extensions.DependencyInjection;
using EasyRequestHandlers.Request;
using System.Collections.Generic;
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
    
    [Fact]
    public void AddAssemblyInfo_MakesInternalsVisible()
    {
        // This test is mainly a reminder to add the AssemblyInfo.cs file
        // with [assembly: InternalsVisibleTo("EasyRequestHandlers.Tests")]
        Assert.True(true);
    }
}